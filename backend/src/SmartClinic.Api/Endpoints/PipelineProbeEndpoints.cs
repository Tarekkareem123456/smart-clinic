namespace SmartClinic.Api.Endpoints;

/// <summary>
/// T-005 — pipeline-verification placeholders ONLY. These are NOT business
/// endpoints and carry no domain meaning; they exist solely so the
/// staff/portal OpenAPI separation mechanism
/// (<see cref="SmartClinic.Api.OpenApi.SurfaceDocumentInclusion"/>) has
/// something real to route — mirroring T-001's placeholder main.ts and
/// T-004's empty Baseline migration. Safe to delete once real endpoints
/// exist under /api/v1/... and /api/v1/portal/....
/// </summary>
public static class PipelineProbeEndpoints
{
    public static WebApplication MapPipelineProbeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/_t005-probe/staff-only", () => Results.Ok(new { surface = "staff" }))
            .WithName("T005ProbeStaffOnly");

        app.MapGet("/api/v1/portal/_t005-probe/portal-only", () => Results.Ok(new { surface = "portal" }))
            .WithName("T005ProbePortalOnly");

        return app;
    }
}
