// Foundation placeholder host for T-001, extended by T-004 (Migrator is a
// separate project, not referenced here), T-005 (OpenAPI separation) and
// T-006 (centralized error-handling). Still no authentication, no
// business functionality.
using SmartClinic.Api.Endpoints;
using SmartClinic.Api.Errors;
using SmartClinic.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSmartClinicOpenApi();

// T-006: the ONE centralized error-handling layer. AddProblemDetails() is
// deliberately NOT called — the required envelope shape
// (status/code/message/fields/correlationId) is not RFC 7807
// ProblemDetails, so GlobalExceptionHandler always fully handles the
// response itself (returns true) rather than falling back to it.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Must be one of the first middlewares so it wraps everything downstream.
app.UseExceptionHandler();

// T-006: covers the other half of "every API error" — responses with an
// error status code but no body and no thrown exception (e.g. an
// unmatched route/method). Reuses the exact same envelope writer as
// GlobalExceptionHandler, so there is one JSON shape everywhere.
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var status = (System.Net.HttpStatusCode)httpContext.Response.StatusCode;
    var code = ErrorCatalog.DefaultCodeForStatus(status);
    var (_, message) = ErrorCatalog.Resolve(code);

    await ErrorEnvelopeWriter.WriteAsync(httpContext, status, code, message, fields: null);
});

app.UseSmartClinicOpenApi();
app.MapPipelineProbeEndpoints();
app.MapErrorHandlingProbeEndpoints();

app.Run();
