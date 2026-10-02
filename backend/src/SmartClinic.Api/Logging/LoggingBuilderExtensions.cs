namespace SmartClinic.Api.Logging;

public static class LoggingBuilderExtensions
{
    /// <summary>
    /// T-007 — call once from <c>Program.cs</c> (<c>builder.Logging.AddSmartClinicLogging()</c>).
    ///
    /// ASP.NET Core 8's ExceptionHandlerMiddleware writes its own log entry
    /// containing the RAW exception before it runs IExceptionHandler. That
    /// entry would bypass redaction. GlobalExceptionHandler already logs the
    /// same exception (type, redacted message, stack trace, correlationId),
    /// so the framework's raw duplicate is switched off rather than left as
    /// an unredacted second copy.
    ///
    /// <c>Microsoft.AspNetCore.Hosting.Diagnostics</c> is also silenced to
    /// suppress the framework's per-request start/finish log entries
    /// (e.g. "Request starting HTTP/1.1 GET …" / "Request finished …"),
    /// which are handled by SmartClinic's own request logging pipeline.
    /// </summary>
    public static ILoggingBuilder AddSmartClinicLogging(this ILoggingBuilder logging) =>
        logging
            .AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None)
            .AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
}
