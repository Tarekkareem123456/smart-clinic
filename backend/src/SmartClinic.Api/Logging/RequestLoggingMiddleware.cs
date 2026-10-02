using System.Diagnostics;

namespace SmartClinic.Api.Logging;

/// <summary>
/// T-007 — the single source of the per-request correlation ID.
/// It is <see cref="HttpContext.TraceIdentifier"/>: the identifier ASP.NET
/// Core already assigns once per request and keeps stable for its whole
/// lifetime. T-006's error envelope already reads the same property, so the
/// response <c>correlationId</c>, the log scope and every log entry agree.
/// No second correlation mechanism exists, and no client-supplied ID is
/// trusted (that would allow log forging and is a public-contract change).
/// </summary>
public static class RequestCorrelation
{
    public const string LogPropertyName = "CorrelationId";

    public static string GetId(HttpContext httpContext) => httpContext.TraceIdentifier;
}

/// <summary>
/// T-007 — outermost request middleware (registered BEFORE the T-006
/// exception handler). It:
/// 1. opens a logging scope carrying <c>CorrelationId</c>, so every log
///    entry written while the request runs (application code, T-006's
///    GlobalExceptionHandler, framework) carries that ID;
/// 2. writes one sanitized request-summary entry after the response.
///
/// It never writes an error response (T-006 owns that), never reads the
/// request body, and never logs raw headers or the raw query string.
/// Everything it logs is copied into plain strings/numbers first — no
/// HttpContext/Request/Response/Headers object is ever placed into log
/// state or a scope, so nothing request-scoped can be retained by a log
/// provider and touched after the request is disposed.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Materialize everything up front as plain strings.
        var correlationId = RequestCorrelation.GetId(context);
        var method = LogRedactor.RedactText(context.Request.Method);
        var path = LogRedactor.RedactText(context.Request.Path.Value);
        var query = LogRedactor.RedactQuery(context.Request.Query);
        var headers = LogRedactor.SafeHeaders(context.Request.Headers);

        var scopeState = new Dictionary<string, object?>
        {
            [RequestCorrelation.LogPropertyName] = correlationId,
        };

        using (_logger.BeginScope(scopeState))
        {
            var stopwatch = Stopwatch.StartNew();
            var statusCode = StatusCodes.Status500InternalServerError; // stays 500 if an exception escapes

            try
            {
                await _next(context);
                statusCode = context.Response.StatusCode;
            }
            finally
            {
                stopwatch.Stop();
                var level = statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

                _logger.Log(
                    level,
                    "HTTP {Method} {Path}{Query} responded {StatusCode} in {ElapsedMs}ms. Headers: {Headers}",
                    method, path, query, statusCode, stopwatch.ElapsedMilliseconds, headers);
            }
        }
    }
}

public static class RequestLoggingApplicationBuilderExtensions
{
    /// <summary>Register BEFORE <c>UseExceptionHandler</c> so the scope also covers T-006's handler.</summary>
    public static IApplicationBuilder UseSmartClinicRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
