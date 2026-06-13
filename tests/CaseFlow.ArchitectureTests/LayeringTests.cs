using System.Reflection;
using NetArchTest.Rules;

namespace CaseFlow.ArchitectureTests;

// The dependency rules, enforced rather than hoped for. The csproj references
// already make most violations fail to compile; these catch the subtler ones -
// a stray using, a domain type reaching for EF Core - that a project reference
// alone would still allow.
public class LayeringTests
{
    // Fully-qualified markers: several projects share type names
    // (DependencyInjection, Program), so unambiguous references matter here.
    private static readonly Assembly Domain = typeof(global::CaseFlow.Domain.Cases.Case).Assembly;
    private static readonly Assembly Application = typeof(global::CaseFlow.Application.Abstractions.ICaseRepository).Assembly;
    private static readonly Assembly Contracts = typeof(global::CaseFlow.Contracts.Cases.CaseResponse).Assembly;
    private static readonly Assembly Api = typeof(global::CaseFlow.Api.Auth.CaseRoles).Assembly;

    private const string DomainNs = "CaseFlow.Domain";
    private const string ApplicationNs = "CaseFlow.Application";
    private const string InfrastructureNs = "CaseFlow.Infrastructure";
    private const string ApiNs = "CaseFlow.Api";
    private const string ContractsNs = "CaseFlow.Contracts";

    [Fact]
    public void Domain_depends_on_nothing_else_in_the_solution()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, ContractsNs)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Domain_is_persistence_ignorant()
    {
        // No ORM, driver, or scheduler types leak into the domain model.
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Npgsql", "Dapper", "Hangfire", "Quartz")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Application_does_not_depend_on_the_ORM()
    {
        // Persistence is reached through the repository/query abstractions, not
        // EF Core directly. (Dapper and EF both live behind Infrastructure.)
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Dapper")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Contracts_are_standalone()
    {
        // The public wire contracts must not drag in domain, application, or
        // infrastructure types - that is the whole point of a separate package.
        var result = Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNs, ApplicationNs, InfrastructureNs, ApiNs)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Controllers_go_through_the_application_layer_not_infrastructure()
    {
        // Controllers depend on application handlers and contracts only - never
        // on the DbContext, repositories, or any other infrastructure type.
        var result = Types.InAssembly(Api)
            .That()
            .ResideInNamespace("CaseFlow.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        AssertSuccess(result);
    }

    private static void AssertSuccess(TestResult result)
    {
        Assert.True(
            result.IsSuccessful,
            result.IsSuccessful
                ? string.Empty
                : "Architecture rule violated by: " + string.Join(", ", result.FailingTypeNames));
    }
}
