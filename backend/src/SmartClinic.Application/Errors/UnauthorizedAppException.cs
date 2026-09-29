namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — authentication is required (maps to HTTP 401 via
/// SmartClinic.Api.Errors.ErrorCatalog, which owns the actual user-facing
/// message). Reserved for when authentication exists; nothing currently
/// throws this (no auth middleware is wired yet — a later task, not
/// T-006/T-007). <paramref name="message"/> is diagnostic/internal only —
/// see <see cref="AppException"/>.
/// </summary>
public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string? message = null)
        : base(ErrorCodes.Unauthorized, message)
    {
    }
}
