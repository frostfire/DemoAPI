using CaseFlow.Domain.Cases;
using CaseFlow.Domain.Cases.Events;
using CaseFlow.Domain.Exceptions;

namespace CaseFlow.UnitTests.Cases;

// These tests pin the workflow itself: which transitions are legal, what each
// one records, and that every illegal move throws instead of silently
// corrupting state. No mocks, no database - the state machine is plain code.
public class CaseWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static Case NewCase() =>
        Case.Create("org_demo_001", "user_writer", "Missing beneficiary form", "Client submitted an incomplete form.", CasePriority.Normal, Now);

    private static Case PendingCase()
    {
        var @case = NewCase();
        @case.Submit("user_writer", Now);
        return @case;
    }

    [Fact]
    public void New_case_starts_as_draft()
    {
        var @case = NewCase();

        Assert.Equal(CaseStatus.Draft, @case.Status);
        Assert.Equal(Now, @case.CreatedAt);
        Assert.Null(@case.SubmittedAt);
    }

    [Fact]
    public void Draft_can_be_submitted()
    {
        var @case = NewCase();

        @case.Submit("user_writer", Now);

        Assert.Equal(CaseStatus.PendingReview, @case.Status);
        Assert.Equal(Now, @case.SubmittedAt);
    }

    [Fact]
    public void Pending_case_can_be_approved()
    {
        var @case = PendingCase();

        @case.Approve("user_approver", Now);

        Assert.Equal(CaseStatus.Approved, @case.Status);
        Assert.Equal(Now, @case.ReviewedAt);
    }

    [Fact]
    public void Pending_case_can_be_rejected_and_keeps_the_reason()
    {
        var @case = PendingCase();

        @case.Reject("user_reviewer", "Form is missing a signature.", Now);

        Assert.Equal(CaseStatus.Rejected, @case.Status);
        Assert.Equal("Form is missing a signature.", @case.RejectReason);
    }

    [Fact]
    public void Rejected_case_can_be_reopened_and_resubmitted()
    {
        var @case = PendingCase();
        @case.Reject("user_reviewer", "Incomplete.", Now);

        @case.Reopen("user_writer", Now);
        Assert.Equal(CaseStatus.Reopened, @case.Status);

        @case.Submit("user_writer", Now);
        Assert.Equal(CaseStatus.PendingReview, @case.Status);
    }

    [Fact]
    public void Approved_case_can_be_archived()
    {
        var @case = PendingCase();
        @case.Approve("user_approver", Now);

        @case.Archive("user_admin", Now);

        Assert.Equal(CaseStatus.Archived, @case.Status);
    }

    [Fact]
    public void Draft_cannot_be_approved()
    {
        var @case = NewCase();

        var ex = Assert.Throws<InvalidCaseTransitionException>(() => @case.Approve("user_approver", Now));

        Assert.Equal(CaseStatus.Draft, ex.CurrentStatus);
        Assert.Equal("approved", ex.AttemptedAction);
    }

    [Fact]
    public void Draft_cannot_be_rejected()
    {
        var @case = NewCase();

        Assert.Throws<InvalidCaseTransitionException>(() => @case.Reject("user_reviewer", "No.", Now));
    }

    [Fact]
    public void Approved_case_cannot_be_rejected()
    {
        var @case = PendingCase();
        @case.Approve("user_approver", Now);

        Assert.Throws<InvalidCaseTransitionException>(() => @case.Reject("user_reviewer", "Too late.", Now));
    }

    [Fact]
    public void Approved_case_cannot_be_reopened()
    {
        var @case = PendingCase();
        @case.Approve("user_approver", Now);

        Assert.Throws<InvalidCaseTransitionException>(() => @case.Reopen("user_writer", Now));
    }

    [Fact]
    public void Pending_case_cannot_be_submitted_again()
    {
        var @case = PendingCase();

        Assert.Throws<InvalidCaseTransitionException>(() => @case.Submit("user_writer", Now));
    }

    [Fact]
    public void Archived_case_accepts_no_further_transitions()
    {
        var @case = PendingCase();
        @case.Approve("user_approver", Now);
        @case.Archive("user_admin", Now);

        Assert.Throws<InvalidCaseTransitionException>(() => @case.Submit("user_writer", Now));
        Assert.Throws<InvalidCaseTransitionException>(() => @case.Approve("user_approver", Now));
        Assert.Throws<InvalidCaseTransitionException>(() => @case.Reject("user_reviewer", "No.", Now));
        Assert.Throws<InvalidCaseTransitionException>(() => @case.Reopen("user_writer", Now));
        Assert.Throws<InvalidCaseTransitionException>(() => @case.Archive("user_admin", Now));
    }

    [Fact]
    public void Details_can_be_edited_in_draft_but_not_in_review()
    {
        var @case = NewCase();
        @case.UpdateDetails("user_writer", "Updated title", null, CasePriority.High, Now);
        Assert.Equal("Updated title", @case.Title);

        @case.Submit("user_writer", Now);

        Assert.Throws<InvalidCaseTransitionException>(
            () => @case.UpdateDetails("user_writer", "Too late", null, CasePriority.Low, Now));
    }

    [Fact]
    public void Only_drafts_can_be_deleted()
    {
        var draft = NewCase();
        Assert.True(draft.CanBeDeleted);

        var pending = PendingCase();
        Assert.False(pending.CanBeDeleted);
    }

    [Fact]
    public void Submitter_cannot_approve_their_own_case()
    {
        var @case = PendingCase(); // submitted by user_writer

        Assert.Throws<SelfApprovalNotAllowedException>(() => @case.Approve("user_writer", Now));

        // The failed approval must not have moved the case.
        Assert.Equal(CaseStatus.PendingReview, @case.Status);
        Assert.Null(@case.ReviewedByUserId);
    }

    [Fact]
    public void A_different_user_can_approve_after_resubmission_by_the_original_approver()
    {
        // user_a submits, user_b rejects, user_b reopens-and-resubmits paths
        // still respect the rule: whoever did the *latest* submit is blocked.
        var @case = NewCase();
        @case.Submit("user_a", Now);
        @case.Reject("user_b", "Fix the form.", Now);
        @case.Reopen("user_writer", Now);
        @case.Submit("user_b", Now);

        Assert.Throws<SelfApprovalNotAllowedException>(() => @case.Approve("user_b", Now));
        @case.Approve("user_a", Now);

        Assert.Equal(CaseStatus.Approved, @case.Status);
        Assert.Equal("user_a", @case.ReviewedByUserId);
    }

    [Fact]
    public void Workflow_records_who_did_what()
    {
        var @case = NewCase();
        @case.Submit("user_writer", Now);
        @case.Approve("user_approver", Now);

        Assert.Equal("user_writer", @case.CreatedByUserId);
        Assert.Equal("user_writer", @case.SubmittedByUserId);
        Assert.Equal("user_approver", @case.ReviewedByUserId);
    }

    [Fact]
    public void Each_transition_raises_its_domain_event()
    {
        var @case = NewCase();
        @case.Submit("user_writer", Now);
        @case.Approve("user_approver", Now);

        Assert.Collection(
            @case.DomainEvents,
            e => Assert.IsType<CaseCreated>(e),
            e => Assert.IsType<CaseSubmitted>(e),
            e =>
            {
                var approved = Assert.IsType<CaseApproved>(e);
                Assert.Equal("user_approver", approved.PerformedByUserId);
                Assert.Equal("user_writer", approved.SubmittedByUserId);
            });
    }

    [Fact]
    public void A_failed_transition_raises_no_event()
    {
        var @case = NewCase();
        @case.ClearDomainEvents();

        Assert.Throws<InvalidCaseTransitionException>(() => @case.Approve("user_approver", Now));

        Assert.Empty(@case.DomainEvents);
    }

    [Fact]
    public void Reject_event_carries_the_reason()
    {
        var @case = PendingCase();
        @case.ClearDomainEvents();

        @case.Reject("user_reviewer", "Signature is missing.", Now);

        var rejected = Assert.IsType<CaseRejected>(Assert.Single(@case.DomainEvents));
        Assert.Equal("Signature is missing.", rejected.Reason);
        Assert.Equal("user_writer", rejected.SubmittedByUserId);
    }
}
