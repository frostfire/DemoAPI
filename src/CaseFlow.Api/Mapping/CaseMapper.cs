using CaseFlow.Application.Cases.Queries;
using CaseFlow.Contracts.Cases;
using Riok.Mapperly.Abstractions;

namespace CaseFlow.Api.Mapping;

// Mapperly generates these at compile time - no runtime reflection, and a
// missing member is a build error instead of a production surprise.
// (Alternatives if needs change: AutoMapper, Mapster, or plain hand-written
// mapping; for a contract surface this small any of them would do.)
[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class CaseMapper
{
    [MapperIgnoreSource(nameof(Domain.Cases.Case.CanBeDeleted))]
    [MapperIgnoreSource(nameof(Domain.Cases.Case.Version))]
    [MapperIgnoreSource(nameof(Domain.Cases.Case.DomainEvents))]
    public static partial CaseResponse ToResponse(this Domain.Cases.Case @case);

    public static partial CaseResponse ToResponse(this CaseSummary summary);

    [MapperIgnoreSource(nameof(Domain.Cases.CaseAuditEntry.CaseId))]
    [MapperIgnoreSource(nameof(Domain.Cases.CaseAuditEntry.OrganizationId))]
    public static partial CaseAuditEntryResponse ToResponse(this Domain.Cases.CaseAuditEntry entry);

    public static partial Domain.Cases.CasePriority ToDomain(this CasePriority priority);

    public static partial Domain.Cases.CaseStatus ToDomain(this CaseStatus status);

    // Weak ETag carrying the Postgres row version; pairs with If-Match.
    public static string ToETag(this Domain.Cases.Case @case) => $"W/\"{@case.Version}\"";
}
