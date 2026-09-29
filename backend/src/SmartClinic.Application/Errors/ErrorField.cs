namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — one field-level detail attached to a <see cref="ValidationAppException"/>.
/// <c>Code</c> is a short machine-readable reason (e.g. "REQUIRED",
/// "INVALID_FORMAT"); <c>Message</c> is optional human text. Neither may
/// contain internal implementation details (SQL, table/column names,
/// stack traces).
/// </summary>
public sealed record ErrorField(string Name, string Code, string? Message = null);
