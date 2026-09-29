using System.Net;
using SmartClinic.Application.Errors;

namespace SmartClinic.Api.Errors;

/// <summary>
/// T-006 — the single piece of code that actually writes an error
/// envelope onto an HTTP response. Both <see cref="GlobalExceptionHandler"/>
/// (thrown exceptions) and the <c>UseStatusCodePages</c> callback in
/// Program.cs (unmatched routes/methods — no exception involved) call this
/// same method, so there is exactly one place in the codebase that knows
/// the envelope's JSON shape.
/// </summary>
public static class ErrorEnvelopeWriter
{
    public static Task WriteAsync(
        HttpContext httpContext,
        HttpStatusCode status,
        string code,
        string message,
        IReadOnlyList<ErrorField>? fields,
        CancellationToken cancellationToken = default)
    {
        httpContext.Response.StatusCode = (int)status;
        httpContext.Response.ContentType = "application/json";

        var envelope = new ErrorEnvelope
        {
            Status = (int)status,
            Code = code,
            Message = message,
            Fields = (fields ?? Array.Empty<ErrorField>())
                .Select(f => new ErrorFieldDto(f.Name, f.Code, f.Message))
                .ToList(),
            // The SAME identifier ASP.NET Core already assigns per request
            // and that any current logging for this request already uses.
            // T-006 deliberately reuses this rather than introducing a new
            // correlation mechanism — that is T-007's scope.
            CorrelationId = httpContext.TraceIdentifier,
        };

        return httpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
    }
}
