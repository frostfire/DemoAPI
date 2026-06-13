using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Cases.Handlers;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Common;
using CaseFlow.Domain.Cases;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CaseFlow.Application;

public static class DependencyInjection
{
    // Handlers are registered one by one instead of by assembly scanning.
    // Nine explicit lines beat a reflection scan nobody can step through,
    // at least at this size - revisit if the count gets silly.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // IOptions<...> resolves even when a host does not bind the section, so
        // handlers can depend on these unconditionally.
        services.AddOptions<Cases.WorkflowOptions>();
        services.AddOptions<Demo.DemoOptions>();

        services.AddScoped<ICommandHandler<CreateCaseCommand, Case>, CreateCaseHandler>();
        services.AddScoped<ICommandHandler<UpdateCaseCommand, Case>, UpdateCaseHandler>();
        services.AddScoped<ICommandHandler<DeleteCaseCommand, bool>, DeleteCaseHandler>();
        services.AddScoped<ICommandHandler<SubmitCaseCommand, Case>, SubmitCaseHandler>();
        services.AddScoped<ICommandHandler<ApproveCaseCommand, Case>, ApproveCaseHandler>();
        services.AddScoped<ICommandHandler<RejectCaseCommand, Case>, RejectCaseHandler>();
        services.AddScoped<ICommandHandler<ReopenCaseCommand, Case>, ReopenCaseHandler>();
        services.AddScoped<ICommandHandler<ArchiveCaseCommand, bool>, ArchiveCaseHandler>();
        services.AddScoped<IQueryHandler<GetCaseQuery, Case>, GetCaseHandler>();
        services.AddScoped<IQueryHandler<SearchCasesQuery, PagedResult<CaseSummary>>, SearchCasesHandler>();
        services.AddScoped<IQueryHandler<GetCaseAuditQuery, IReadOnlyList<CaseAuditEntry>>, GetCaseAuditHandler>();

        return services;
    }
}
