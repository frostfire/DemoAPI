namespace CaseFlow.Api.Auth;

public static class CaseRoles
{
    public const string Reader = "CaseReader";
    public const string Writer = "CaseWriter";
    public const string Reviewer = "CaseReviewer";
    public const string Approver = "CaseApprover";
    public const string Admin = "CaseAdmin";

    public static readonly string[] All = [Reader, Writer, Reviewer, Approver, Admin];
}
