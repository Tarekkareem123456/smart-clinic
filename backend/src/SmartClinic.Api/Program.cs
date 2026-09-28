// Foundation placeholder host for T-001, extended by T-004 (Migrator is a
// separate project, not referenced here) and T-005 (OpenAPI separation).
// Still no authentication, no business functionality.
using SmartClinic.Api.Endpoints;
using SmartClinic.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSmartClinicOpenApi();

var app = builder.Build();

app.UseSmartClinicOpenApi();
app.MapPipelineProbeEndpoints();

app.Run();
