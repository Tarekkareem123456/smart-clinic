# Audit test harness (T-104)

`SmartClinic.Configuration.Tests` contains a reflection-based CI guard for
application use cases. CI already runs this test project in the
**Backend build + unit tests** job.

## Discovery and required coverage

- An application use case is a public instance method declared on a concrete
  class in `SmartClinic.Application.*` whose class name ends in `Service`.
- Every such method must be marked `[UseCaseOperation(ReadOnly)]` or
  `[UseCaseOperation(Mutating)]`. An unclassified method fails the test.
- Every method marked `Mutating` must be referenced by an `[AuditCoverage]`
  marker on an actual test method. The marker identifies the service method
  and full parameter signature, so overloads remain distinct.
- Coverage markers that point to a missing or non-mutating use case also fail.

The test theory builds its cases from the application assembly at test-discovery
time. The current inventory contains
`ConfigurationService.SetAsync(string, string, CancellationToken)` and its
audit test is
`ConfigurationServiceTests.Set_writes_value_and_records_audit_for_non_invariant`.
When a module or service is added, the inventory is discovered automatically;
each newly classified mutating method appears as a separate CI test case and
fails until a matching audit test is marked.

## Adding or changing a use case

1. Put public application use cases on a concrete `*Service` in
   `SmartClinic.Application.*` and classify each public instance method.
2. For a mutating method, add a test that verifies the audit writer received
   the expected event and mark that test with `[AuditCoverage(...)]`, including
   the method's parameter types.
3. Run `dotnet test backend/tests/SmartClinic.Configuration.Tests/SmartClinic.Configuration.Tests.csproj -c Release`.

The marker is a discoverability link, not a substitute for meaningful test
assertions. The linked test must verify that the operation records the expected
audit data.
