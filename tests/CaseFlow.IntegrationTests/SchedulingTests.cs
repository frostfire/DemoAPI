using System.Net;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace CaseFlow.IntegrationTests;

// Proves the API side of the scheduling showcase: approving a case enqueues a
// delayed Hangfire job. The job itself runs in the worker's Hangfire server
// (not started in tests), so here we assert it was scheduled, against the real
// Hangfire storage in the Testcontainers database.
public class SchedulingTests(CaseFlowApiFactory factory) : IClassFixture<CaseFlowApiFactory>
{
    [Fact]
    public async Task Approving_a_case_schedules_an_auto_archive_job()
    {
        var org = "org_scheduling";
        var writer = await factory.AsUserAsync(["CaseWriter"], org);
        var created = await writer.CreateCaseAsync("Scheduling case");
        await writer.PostAsync($"/api/v1/cases/{created.Id}/submit", null);

        var approver = await factory.AsUserAsync(["CaseApprover"], org);
        var approve = await approver.PostAsync($"/api/v1/cases/{created.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // A delayed job referencing this case should now be in Hangfire's
        // scheduled set.
        var storage = factory.Services.GetRequiredService<JobStorage>();
        var scheduled = storage.GetMonitoringApi().ScheduledJobs(0, 100);

        Assert.Contains(scheduled, job =>
            job.Value.Job.Args.Any(arg => arg?.ToString() == created.Id.ToString()));
    }
}
