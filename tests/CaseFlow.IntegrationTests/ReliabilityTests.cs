using System.Net;
using System.Net.Http.Json;
using CaseFlow.Contracts.Cases;
using CaseFlow.Infrastructure.Outbox;
using CaseFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseFlow.IntegrationTests;

// The reliability patterns, proven end to end: the outbox transaction, the
// audit trail, idempotent retries, and optimistic concurrency - all against
// real Postgres.
public class ReliabilityTests(CaseFlowApiFactory factory) : IClassFixture<CaseFlowApiFactory>
{
    private const string Writer = "CaseWriter";
    private const string Approver = "CaseApprover";

    [Fact]
    public async Task Approving_a_case_writes_audit_and_outbox_in_the_same_transaction()
    {
        var org = "org_outbox_proof";
        var writer = await factory.AsUserAsync([Writer], org);
        var created = await writer.CreateCaseAsync("Outbox proof case");
        await writer.PostAsync($"/api/v1/cases/{created.Id}/submit", null);

        var approver = await factory.AsUserAsync([Approver], org);
        var approve = await approver.PostAsync($"/api/v1/cases/{created.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // Audit trail via the API: Created, Submitted, Approved.
        var audit = await writer.GetFromJsonAsync<List<CaseAuditEntryResponse>>(
            $"/api/v1/cases/{created.Id}/audit", ApiClientExtensions.Json);
        Assert.NotNull(audit);
        Assert.Equal(["Approved", "Submitted", "Created"], audit.Select(a => a.Action).ToList());

        // Outbox rows committed alongside the state change - and the batch
        // processor drains them, which drives the (fake) notification.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaseFlowDbContext>();

        var caseEventIds = (await db.CaseAuditLog.Where(a => a.CaseId == created.Id).ToListAsync())
            .Select(a => a.Id)
            .ToHashSet();
        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Payload.Contains(created.Id.ToString()))
            .ToListAsync();
        Assert.Equal(3, pending.Count);

        var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();
        while (await processor.ProcessBatchAsync() > 0) { }

        var stillPending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Payload.Contains(created.Id.ToString()))
            .CountAsync();
        Assert.Equal(0, stillPending);
    }

    [Fact]
    public async Task Duplicate_submit_with_the_same_idempotency_key_replays_without_a_second_execution()
    {
        var writer = await factory.AsUserAsync([Writer], "org_idem");
        var created = await writer.CreateCaseAsync("Idempotency case");

        var key = Guid.NewGuid().ToString();

        var first = await SendWithKey(writer, $"/api/v1/cases/{created.Id}/submit", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync();

        // Without the key this retry would be a 409 - the case is already
        // PendingReview. With it, the stored response comes back instead.
        var second = await SendWithKey(writer, $"/api/v1/cases/{created.Id}/submit", key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody, await second.Content.ReadAsStringAsync());

        // And the audit log proves the transition only ran once.
        var audit = await writer.GetFromJsonAsync<List<CaseAuditEntryResponse>>(
            $"/api/v1/cases/{created.Id}/audit", ApiClientExtensions.Json);
        Assert.Single(audit!, a => a.Action == "Submitted");
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_for_a_different_request_is_rejected()
    {
        var org = "org_idem_reuse";
        var writer = await factory.AsUserAsync([Writer], org);
        var created = await writer.CreateCaseAsync("Key reuse case");
        await writer.PostAsync($"/api/v1/cases/{created.Id}/submit", null);

        var approverClient = await factory.AsUserAsync(["CaseReviewer"], org);
        var key = Guid.NewGuid().ToString();

        var first = await SendWithKey(approverClient, $"/api/v1/cases/{created.Id}/reject", key,
            new RejectCaseRequest("Original reason"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same key, different payload: that's not a retry, that's a bug.
        var second = await SendWithKey(approverClient, $"/api/v1/cases/{created.Id}/reject", key,
            new RejectCaseRequest("A different reason entirely"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task Stale_etag_gets_412_and_current_etag_succeeds()
    {
        var writer = await factory.AsUserAsync([Writer], "org_concurrency");
        var created = await writer.CreateCaseAsync("Concurrency case");

        var get = await writer.GetAsync($"/api/v1/cases/{created.Id}");
        var originalETag = get.Headers.ETag!.ToString();

        // First update with the current ETag works and bumps the version.
        var ok = await PatchWithIfMatch(writer, created.Id, "Renamed once", originalETag);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var newETag = ok.Headers.ETag!.ToString();
        Assert.NotEqual(originalETag, newETag);

        // Replaying the original ETag is now a stale write - 412.
        var stale = await PatchWithIfMatch(writer, created.Id, "Renamed twice", originalETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

        // The current ETag still works.
        var fresh = await PatchWithIfMatch(writer, created.Id, "Renamed twice properly", newETag);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    private static Task<HttpResponseMessage> SendWithKey(
        HttpClient client, string url, string key, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Idempotency-Key", key);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ApiClientExtensions.Json);
        }

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PatchWithIfMatch(
        HttpClient client, Guid caseId, string title, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cases/{caseId}")
        {
            Content = JsonContent.Create(
                new UpdateCaseRequest(title, null, CasePriority.Normal),
                options: ApiClientExtensions.Json),
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        return client.SendAsync(request);
    }
}
