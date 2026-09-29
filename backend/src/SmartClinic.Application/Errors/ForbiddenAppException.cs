namespace SmartClinic.Application.Errors;

/// <summary>
/// T-006 — the caller is authenticated but not permitted (maps to HTTP
/// 403 via SmartClinic.Api.Errors.ErrorCatalog, which owns the actual
/// user-facing message). Reserved for when authorization exists; nothing
/// currently throws this (a later task). <paramref name="message"/> is
/// diagnostic/internal only — see <see cref="AppException"/>.
/// </summary>
public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string? message = null)
        : base(ErrorCodes.Forbidden, message)
    {
    }
}
