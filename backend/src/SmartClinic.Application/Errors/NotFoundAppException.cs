namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — a requested resource does not exist (maps to HTTP 404 via
/// SmartClinic.Api.Errors.ErrorCatalog, which owns the actual user-facing
/// message). <paramref name="message"/> is diagnostic/internal only — see
/// <see cref="AppException"/>.
/// </summary>
public sealed class NotFoundAppException : AppException
{
    public NotFoundAppException(string? message = null)
        : base(ErrorCodes.NotFound, message)
    {
    }
}
