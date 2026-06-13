using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Cases.Queries;
using FluentValidation;

namespace CaseFlow.Application.Cases.Validators;

// Validation sits at the application boundary: handlers refuse bad commands
// before the domain ever sees them. Enum range checks are not needed here -
// the API layer rejects unknown enum values during model binding.

public sealed class CreateCaseCommandValidator : AbstractValidator<CreateCaseCommand>
{
    public CreateCaseCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(4000);
    }
}

public sealed class UpdateCaseCommandValidator : AbstractValidator<UpdateCaseCommand>
{
    public UpdateCaseCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(4000);
    }
}

public sealed class RejectCaseCommandValidator : AbstractValidator<RejectCaseCommand>
{
    public RejectCaseCommandValidator()
    {
        // A rejection without a reason is useless to the person who has to fix
        // the case, so the reason is mandatory at the boundary.
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class SearchCasesQueryValidator : AbstractValidator<SearchCasesQuery>
{
    private static readonly string[] AllowedSorts =
        ["createdAt", "-createdAt", "title", "-title", "priority", "-priority", "status", "-status"];

    public SearchCasesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.SearchTerm).MaximumLength(200);
        RuleFor(q => q.Sort)
            .Must(s => AllowedSorts.Contains(s))
            .WithMessage($"Sort must be one of: {string.Join(", ", AllowedSorts)}.");
    }
}
