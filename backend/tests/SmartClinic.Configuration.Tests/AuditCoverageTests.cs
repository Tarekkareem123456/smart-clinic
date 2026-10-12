using System.Reflection;
using SmartClinic.Application.Configuration;
using SmartClinic.Application.UseCases;
using Xunit;

namespace SmartClinic.Configuration.Tests;

public sealed class AuditCoverageTests
{
    private static readonly Assembly ApplicationAssembly = typeof(ConfigurationService).Assembly;
    private static readonly Assembly TestAssembly = typeof(AuditCoverageTests).Assembly;

    public static IEnumerable<object[]> MutatingUseCases()
        => DiscoverUseCases()
            .Where(useCase => useCase.Operation.Kind == UseCaseOperationKind.Mutating)
            .Select(useCase => new object[]
            {
                useCase.Method.DeclaringType!.FullName!,
                FormatMethod(useCase.Method)
            });

    [Fact]
    public void Every_public_application_service_method_has_an_explicit_operation_kind()
    {
        var unclassified = DiscoverServiceMethods()
            .Where(method => method.GetCustomAttribute<UseCaseOperationAttribute>() is null)
            .Select(FormatMethod)
            .ToArray();

        Assert.True(
            unclassified.Length == 0,
            "Classify every public instance method on an application *Service as ReadOnly or Mutating. " +
            $"Unclassified methods: {string.Join(", ", unclassified)}");
    }

    [Theory]
    [MemberData(nameof(MutatingUseCases))]
    public void Every_mutating_use_case_has_a_matching_audit_test(
        string serviceTypeName,
        string methodSignature)
    {
        var useCase = DiscoverUseCases().Single(candidate =>
            candidate.Method.DeclaringType!.FullName == serviceTypeName &&
            FormatMethod(candidate.Method) == methodSignature);

        var coverage = DiscoverAuditCoverage();
        var matchingTests = coverage
            .Where(item => Matches(item.Attribute, useCase.Method))
            .Select(item => item.TestMethod)
            .ToArray();

        Assert.True(
            matchingTests.Length > 0,
            $"Mutating use case {methodSignature} has no test marked with [AuditCoverage].");
    }

    [Fact]
    public void Audit_coverage_attributes_must_point_to_real_mutating_use_cases()
    {
        var invalid = DiscoverAuditCoverage()
            .Where(item => !DiscoverUseCases().Any(useCase =>
                useCase.Operation.Kind == UseCaseOperationKind.Mutating &&
                Matches(item.Attribute, useCase.Method)))
            .Select(item => $"{item.TestMethod.DeclaringType?.FullName}.{item.TestMethod.Name}")
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            "Audit coverage markers must reference an existing Mutating application use case. " +
            $"Invalid markers: {string.Join(", ", invalid)}");
    }

    private static IEnumerable<MethodInfo> DiscoverServiceMethods()
        => ApplicationAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => type.Namespace?.StartsWith("SmartClinic.Application", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName);

    private static IEnumerable<DiscoveredUseCase> DiscoverUseCases()
        => DiscoverServiceMethods()
            .Select(method => new
            {
                Method = method,
                Operation = method.GetCustomAttribute<UseCaseOperationAttribute>()
            })
            .Where(useCase => useCase.Operation is not null)
            .Select(useCase => new DiscoveredUseCase(useCase.Method, useCase.Operation!));

    private static IEnumerable<DiscoveredAuditCoverage> DiscoverAuditCoverage()
        => TestAssembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<AuditCoverageAttribute>()
                .Where(_ => method.GetCustomAttribute<FactAttribute>() is not null)
                .Select(attribute => new DiscoveredAuditCoverage(method, attribute)));

    private static bool Matches(AuditCoverageAttribute coverage, MethodInfo method)
        => coverage.UseCaseType == method.DeclaringType &&
           coverage.UseCaseMethod == method.Name &&
           coverage.ParameterTypes.SequenceEqual(
               method.GetParameters().Select(parameter => parameter.ParameterType));

    private static string FormatMethod(MethodInfo method)
        => $"{method.DeclaringType?.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})";

    private sealed record DiscoveredUseCase(
        MethodInfo Method,
        UseCaseOperationAttribute Operation);

    private sealed record DiscoveredAuditCoverage(
        MethodInfo TestMethod,
        AuditCoverageAttribute Attribute);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AuditCoverageAttribute : Attribute
{
    public AuditCoverageAttribute(Type useCaseType, string useCaseMethod, params Type[] parameterTypes)
    {
        UseCaseType = useCaseType;
        UseCaseMethod = useCaseMethod;
        ParameterTypes = parameterTypes;
    }

    public Type UseCaseType { get; }

    public string UseCaseMethod { get; }

    public IReadOnlyList<Type> ParameterTypes { get; }
}
