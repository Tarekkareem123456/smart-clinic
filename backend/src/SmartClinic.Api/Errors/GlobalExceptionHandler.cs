using Microsoft.AspNetCore.Diagnostics;
using SmartClinic.Application.Errors;

namespace SmartClinic.Api.Errors;

/// <summary>
/// T-006 — the single centralized layer every thrown exception passes
/// through (ASP.NET Core's built-in <see cref="IExceptionHandler"/>,
/// registered via <c>AddExceptionHandler&lt;GlobalExceptionHandler&gt;()</c>
/// + <c>app.UseExceptionHandler()</c> in Program.cs). No controller,
/// service, or endpoint is allowed to write its own error response.
///
/// Known <see cref="AppException"/>s are mapped to their catalog status,
/// code and message. Anything else — ANY other exception type — is
/// treated as unknown: the client only ever sees the fixed, generic
/// <see cref="ErrorCatalog.InternalError"/> entry. The exception's real
/// message, type name, and stack trace are logged server-side (never sent
/// to the client), tagged with the same correlationId the response body
/// carries.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        if (exception is AppException appException)
        {
            // An AppException whose code is NOT in the catalog must never
            // surface that code to the client: it is answered with the
            // INTERNAL_ERROR entry instead. The original code is kept for
            // the server-side log line only.
            var isRegistered = ErrorCatalog.Entries.ContainsKey(appException.Code);
            var responseCode = isRegistered ? appException.Code : ErrorCatalog.InternalError;
            var responseFields = isRegistered ? appException.Fields : null;
            var (status, defaultMessage) = ErrorCatalog.Resolve(responseCode);

            // exception.Message (AppException.Message) is diagnostic/internal
            // only, for this log line and whatever else inspects the caught
            // exception server-side. It is NEVER sent to the client — the
            // client only ever sees `defaultMessage` from the catalog. This
            // holds even if a caller passed unsafe/internal text into an
            // AppException's constructor by mistake.
            _logger.LogWarning(
                exception,
                "Handled application error. CorrelationId={CorrelationId} Code={Code} ResponseCode={ResponseCode} Status={Status}",
                correlationId, appException.Code, responseCode, (int)status);

            await ErrorEnvelopeWriter.WriteAsync(
                httpContext, status, responseCode, defaultMessage, responseFields, cancellationToken);
            return true;
        }

        // Unknown/unhandled exception: log the full detail server-side ONLY.
        // The client response below never includes exception.Message,
        // exception.GetType().Name, or exception.StackTrace.
        _logger.LogError(
            exception,
            "Unhandled exception. CorrelationId={CorrelationId}",
            correlationId);

        var (fallbackStatus, fallbackMessage) = ErrorCatalog.Resolve(ErrorCatalog.InternalError);
        await ErrorEnvelopeWriter.WriteAsync(
            httpContext, fallbackStatus, ErrorCatalog.InternalError, fallbackMessage, fields: null, cancellationToken);
        return true;
    }
}
