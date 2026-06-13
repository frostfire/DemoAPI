using CaseFlow.Domain.Cases;
using CaseFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CaseFlow.Infrastructure.Demo;

// Builds the sample data a sandbox visitor lands on: a couple of organizations
// with cases in every workflow state, so the API does something visible the
// moment someone opens Swagger. Used both for first-run seeding and by the
// scheduled reset. Distinct submitter/approver ids keep the seeded approvals
// clear of the self-approval rule.
public sealed class DemoDataSeeder(
    CaseFlowDbContext dbContext,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    private const string Alpha = "org_demo_alpha";
    private const string Beta = "org_demo_beta";
    private const string Writer = "user_demo_writer";
    private const string Reviewer = "user_demo_reviewer";
    private const string Approver = "user_demo_approver";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Cases.AnyAsync(cancellationToken))
        {
            return; // already seeded
        }

        var now = clock.GetUtcNow();

        // org_demo_alpha: one case in each state.
        var draft = Case.Create(Alpha, Writer, "Draft: incomplete intake form", "Client form is missing a signature page.", CasePriority.Normal, now);

        var pending = Case.Create(Alpha, Writer, "Pending: beneficiary change request", "Awaiting reviewer sign-off.", CasePriority.High, now);
        pending.Submit(Writer, now);

        var approved = Case.Create(Alpha, Writer, "Approved: address update", "Routine change, approved.", CasePriority.Low, now);
        approved.Submit(Writer, now);
        approved.Approve(Approver, now);

        var rejected = Case.Create(Alpha, Writer, "Rejected: duplicate claim", "Filed twice; rejected with a reason.", CasePriority.Normal, now);
        rejected.Submit(Writer, now);
        rejected.Reject(Reviewer, "Duplicate of an existing case.", now);

        // org_demo_beta: a single case, so the org-isolation rule is easy to
        // see - a token scoped to alpha cannot read this one.
        var betaCase = Case.Create(Beta, Writer, "Beta org: separate tenant case", "Only visible to org_demo_beta tokens.", CasePriority.Urgent, now);

        dbContext.Cases.AddRange(draft, pending, approved, rejected, betaCase);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Demo data seeded: 5 cases across 2 organizations");
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        // Wipe the application tables and reseed. Hangfire's own tables are left
        // alone - this resets the demo domain data, not the job history.
        await dbContext.CaseAuditLog.ExecuteDeleteAsync(cancellationToken);
        await dbContext.OutboxMessages.ExecuteDeleteAsync(cancellationToken);
        await dbContext.IdempotencyRecords.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Cases.ExecuteDeleteAsync(cancellationToken);

        await SeedAsync(cancellationToken);

        logger.LogInformation("Demo sandbox reset to seed state");
    }
}
