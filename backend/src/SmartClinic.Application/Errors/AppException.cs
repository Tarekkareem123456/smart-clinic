namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — base type for the small set of well-known "application errors":
/// error conditions the Application layer recognizes and names on purpose,
/// as opposed to unexpected/unhandled exceptions. The Api layer's
/// centralized error-handling middleware (<c>GlobalExceptionHandler</c>)
/// is the ONLY place that turns any of these into an HTTP response; no
/// controller/service/endpoint writes its own error response.
///
/// <see cref="Code"/> must be one of <see cref="ErrorCodes"/>.
///
/// IMPORTANT: the base <see cref="Exception.Message"/> on an
/// <see cref="AppException"/> is diagnostic/internal only — it is never
/// sent to the client. <c>GlobalExceptionHandler</c> always sends the
/// catalog's default message for <see cref="Code"/>
/// (<c>SmartClinic.Api.Errors.ErrorCatalog</c>) instead, regardless of
/// what this exception's <c>Message</c> is. This means a caller may safely
/// put internal detail in <c>Message</c> for server-side logs without
/// risking it reaching a client — but conversely, callers must not rely on
/// this <c>Message</c> to communicate anything to the user; it never
/// reaches them. There is deliberately no "safe default message" text
/// duplicated here or in any subclass — the catalog is the single source
/// of truth for user-facing text.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string? message = null, IReadOnlyList<ErrorField>? fields = null)
        : base(message ?? code)
    {
        Code = code;
        Fields = fields ?? Array.Empty<ErrorField>();
    }

    public string Code { get; }

    public IReadOnlyList<ErrorField> Fields { get; }
}
