using System.Net;
using System.Net.Http.Json;
using CaseFlow.Contracts.Cases;
using CaseFlow.Contracts.Common;

namespace CaseFlow.IntegrationTests;

// The tests a security reviewer reads first: who can do what, and above all,
// who cannot. Each one drives the full stack - HTTP, JWT validation, policy
// checks, the application's object-level guard, EF Core, and Postgres in a
// Testcontainers container.
public class AuthorizationTests(CaseFlowApiFactory factory) : IClassFixture<CaseFlowApiFactory>
{
    [Fact]
    public async Task Anonymous_request_gets_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cases");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reader_can_search_but_cannot_create()
    {
        var reader = await factory.AsUserAsync([Roles.Reader]);

        var search = await reader.GetAsync("/api/v1/cases");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var create = await reader.PostAsJsonAsync(
            "/api/v1/cases",
            new CreateCaseRequest("Nope", null, CasePriority.Normal));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Writer_creates_a_case_in_their_own_org_from_the_token()
    {
        var writer = await factory.AsUserAsync([Roles.Writer], organizationId: "org_alpha");

        var created = await writer.CreateCaseAsync();

        Assert.Equal("org_alpha", created.OrganizationId);
        Assert.Equal(CaseStatus.Draft, created.Status);
    }

    [Fact]
    public async Task User_from_another_org_cannot_read_the_case()
    {
        var writerA = await factory.AsUserAsync([Roles.Writer], organizationId: "org_alpha");
        var created = await writerA.CreateCaseAsync();

        var readerB = await factory.AsUserAsync([Roles.Reader], organizationId: "org_beta");
        var crossOrgRead = await readerB.GetAsync($"/api/v1/cases/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, crossOrgRead.StatusCode);

        // Same-org reader sees it fine.
        var readerA = await factory.AsUserAsync([Roles.Reader], organizationId: "org_alpha");
        var sameOrgRead = await readerA.GetAsync($"/api/v1/cases/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, sameOrgRead.StatusCode);
    }

    [Fact]
    public async Task Search_never_leaks_other_orgs_cases()
    {
        var writerA = await factory.AsUserAsync([Roles.Writer], organizationId: "org_gamma");
        await writerA.CreateCaseAsync("Gamma case");

        var readerB = await factory.AsUserAsync([Roles.Reader], organizationId: "org_delta");

        // Asking for another org explicitly must not work for a non-admin.
        var result = await readerB.GetFromJsonAsync<PagedResponse<CaseResponse>>(
            "/api/v1/cases?organizationId=org_gamma", ApiClientExtensions.Json);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Items, c => c.OrganizationId == "org_gamma");
    }

    [Fact]
    public async Task Admin_can_read_across_orgs()
    {
        var writer = await factory.AsUserAsync([Roles.Writer], organizationId: "org_epsilon");
        var created = await writer.CreateCaseAsync();

        var admin = await factory.AsUserAsync([Roles.Admin], organizationId: "org_unrelated");
        var read = await admin.GetAsync($"/api/v1/cases/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task Writer_cannot_approve()
    {
        var writer = await factory.AsUserAsync([Roles.Writer]);
        var created = await writer.CreateCaseAsync();
        await writer.PostAsync($"/api/v1/cases/{created.Id}/submit", null);

        var approve = await writer.PostAsync($"/api/v1/cases/{created.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task Approver_approves_a_submitted_case()
    {
        var org = "org_approval_flow";
        var writer = await factory.AsUserAsync([Roles.Writer], org);
        var created = await writer.CreateCaseAsync();
        await writer.PostAsync($"/api/v1/cases/{created.Id}/submit", null);

        var approver = await factory.AsUserAsync([Roles.Approver], org);
        var approve = await approver.PostAsync($"/api/v1/cases/{created.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var body = await approve.Content.ReadFromJsonAsync<CaseResponse>(ApiClientExtensions.Json);
        Assert.Equal(CaseStatus.Approved, body!.Status);
    }

    [Fact]
    public async Task Submitter_cannot_approve_their_own_case_even_with_the_approver_role()
    {
        // One user holding both roles - the policy lets them try, the domain
        // rule shuts them down.
        var user = await factory.AsUserAsync(
            [Roles.Writer, Roles.Approver],
            organizationId: "org_self_approval",
            userId: "user_wears_two_hats");

        var created = await user.CreateCaseAsync();
        await user.PostAsync($"/api/v1/cases/{created.Id}/submit", null);

        var approve = await user.PostAsync($"/api/v1/cases/{created.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        // A different approver in the same org can still approve it.
        var approver = await factory.AsUserAsync([Roles.Approver], "org_self_approval");
        var secondTry = await approver.PostAsync($"/api/v1/cases/{created.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, secondTry.StatusCode);
    }

    [Fact]
    public async Task Invalid_transition_returns_409_problem_details()
    {
        var writer = await factory.AsUserAsync([Roles.Writer, Roles.Approver], "org_conflict");
        var created = await writer.CreateCaseAsync();

        // Approving a draft - never submitted.
        var approve = await writer.PostAsync($"/api/v1/cases/{created.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        var problem = await approve.Content.ReadFromJsonAsync<ProblemDocument>(ApiClientExtensions.Json);
        Assert.Equal("Invalid case transition", problem!.Title);
        Assert.Contains("Draft", problem.Detail);
    }

    [Fact]
    public async Task Validation_failure_returns_400_with_field_errors()
    {
        var writer = await factory.AsUserAsync([Roles.Writer]);

        var response = await writer.PostAsJsonAsync(
            "/api/v1/cases",
            new CreateCaseRequest("", null, CasePriority.Normal));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDocument>(ApiClientExtensions.Json);
        Assert.Equal("Validation failed", problem!.Title);
        Assert.True(problem.Errors!.ContainsKey("title"));
    }

    [Fact]
    public async Task Demo_token_endpoint_rejects_unknown_roles()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/demo/token",
            new { roles = new[] { "SuperUser" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static class Roles
    {
        public const string Reader = "CaseReader";
        public const string Writer = "CaseWriter";
        public const string Approver = "CaseApprover";
        public const string Admin = "CaseAdmin";
    }

    // Minimal ProblemDetails shape for assertions - keeps the test project
    // free of an MVC dependency just to deserialize an error body.
    private sealed record ProblemDocument(
        string? Title,
        string? Detail,
        int? Status,
        Dictionary<string, string[]>? Errors);
}
