using System.ComponentModel.DataAnnotations;
using SmartClinic.Application.Errors;

namespace SmartClinic.Api.Endpoints;

/// <summary>
/// T-006 — TEMPORARY pipeline-verification placeholders ONLY, exactly like
/// T-005's <see cref="PipelineProbeEndpoints"/> (kept in a separate file so
/// T-005's own file is untouched). These are NOT business endpoints and
/// carry no domain meaning; they exist solely so the centralized
/// error-handling mechanism (<c>GlobalExceptionHandler</c> +
/// <c>ErrorEnvelopeWriter</c>) has real HTTP requests to exercise for the
/// tests in <c>SmartClinic.Api.Tests.ErrorEnvelopeTests</c>. Safe to
/// delete once real endpoints exist that naturally throw
/// <see cref="AppException"/>s or hit real validation.
/// </summary>
public static class ErrorHandlingProbeEndpoints
{
    public static WebApplication MapErrorHandlingProbeEndpoints(this WebApplication app)
    {
        // Known application error -> maps through the catalog to HTTP 404.
        app.MapGet("/api/v1/_t006-probe/known-error", () =>
        {
            throw new NotFoundAppException("Probe resource was not found.");
        });

        // Validation error with field-level detail -> HTTP 400, "fields" populated.
        app.MapPost("/api/v1/_t006-probe/validation-error", (ProbeValidationRequest request) =>
        {
            var context = new ValidationContext(request);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
            {
                var fields = results
                    .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty)
                        .Select(name => new ErrorField(name, "REQUIRED", r.ErrorMessage)))
                    .ToList();

                throw new ValidationAppException(fields);
            }

            return Results.Ok(new { received = request.Name });
        });

        // Known application error whose Message deliberately carries
        // unsafe/internal-looking text (fake SQL fragment + fake table/
        // column name + a marker word). This message must NEVER reach the
        // client, even though it came from a KNOWN AppException (not the
        // unknown-exception path below) — GlobalExceptionHandler always
        // substitutes ErrorCatalog's safe default message instead. See
        // SmartClinic.Api.Tests.ErrorEnvelopeTests.
        //   Known_application_error_with_unsafe_internal_message_never_leaks_it
        app.MapGet("/api/v1/_t006-probe/known-error-with-unsafe-message", () =>
        {
            throw new ConflictAppException(
                "duplicate key value violates unique constraint \"patient_records_ssn_key\" -- ConflictCanary");
        });

        // AppException carrying a code that is NOT registered in
        // ErrorCatalog. The unregistered code must never reach the client;
        // the response must be the INTERNAL_ERROR envelope. See
        // SmartClinic.Api.Tests.ErrorEnvelopeTests.
        //   Unregistered_app_exception_code_is_never_emitted_and_yields_internal_error_envelope
        app.MapGet("/api/v1/_t006-probe/unregistered-code-error", () =>
        {
            throw new UnregisteredCodeProbeException();
        });

        // Unknown/unhandled exception -> must NEVER leak this message,
        // "SELECT", or the fake table name to the client. The generic
        // exception message intentionally looks like something that must
        // not leak (a fake SQL fragment + a fake table name + a marker
        // word), so the tests can assert its absence from the response.
        app.MapGet("/api/v1/_t006-probe/unknown-error", () =>
        {
            throw new InvalidOperationException(
                "SELECT * FROM patient_records WHERE ssn = '...' -- StackTraceCanary");
        });

        return app;
    }
}

/// <summary>Probe-only request shape (T-006), not a domain/business model.</summary>
public sealed class ProbeValidationRequest
{
    [Required(ErrorMessage = "Name is required.")]
    public string? Name { get; set; }
}

/// <summary>Probe-only AppException with a code absent from ErrorCatalog (T-006).</summary>
public sealed class UnregisteredCodeProbeException : AppException
{
    public const string UnregisteredCode = "UNREGISTERED_CODE_CANARY";

    public UnregisteredCodeProbeException()
        : base(UnregisteredCode, "internal detail -- UnregisteredMessageCanary")
    {
    }
}
