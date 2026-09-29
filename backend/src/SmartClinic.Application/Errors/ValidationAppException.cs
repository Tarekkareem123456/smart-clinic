namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — one or more fields failed validation (maps to HTTP 400 via
/// SmartClinic.Api.Errors.ErrorCatalog, which owns the top-level
/// user-facing message). Carries the field-level details.
///
/// Unlike the top-level <paramref name="message"/> (diagnostic/internal
/// only, see <see cref="AppException"/>), each <see cref="ErrorField"/> in
/// <paramref name="fields"/> IS sent to the client as-is — field names,
/// field codes, and any field-level message must therefore already be
/// safe, user-facing validation text (e.g. "Name is required"), never
/// internal detail. This class does not sanitize them; callers are
/// responsible for only ever constructing safe <see cref="ErrorField"/>
/// values (no sanitizer framework is introduced for this — see the T-006
/// report).
/// </summary>
public sealed class ValidationAppException : AppException
{
    public ValidationAppException(IReadOnlyList<ErrorField> fields, string? message = null)
        : base(ErrorCodes.ValidationFailed, message, fields)
    {
    }
}
