namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — the central catalog of error CODE strings that Application-layer
/// exceptions may carry. This is the single source of truth for these
/// literals: nothing outside this class hard-codes an error code string.
///
/// These are pure identifiers with no HTTP knowledge (Application must not
/// depend on ASP.NET Core/HTTP concepts). The Api layer's
/// <c>SmartClinic.Api.Errors.ErrorCatalog</c> maps each of these to an
/// HTTP status and a default user-facing message.
///
/// "INTERNAL_ERROR" and "METHOD_NOT_ALLOWED" are intentionally NOT listed
/// here: they are Api-only fallback codes for cases with no corresponding
/// application exception (unknown exceptions, unmatched routes/methods) —
/// Application code never throws them on purpose.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
}
