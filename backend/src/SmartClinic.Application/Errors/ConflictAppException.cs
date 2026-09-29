namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — the request conflicts with the current state (maps to HTTP 409
/// via SmartClinic.Api.Errors.ErrorCatalog, which owns the actual
/// user-facing message). <paramref name="message"/> is diagnostic/internal
/// only — see <see cref="AppException"/>.
/// </summary>
public sealed class ConflictAppException : AppException
{
    public ConflictAppException(string? message = null)
        : base(ErrorCodes.Conflict, message)
    {
    }
}
