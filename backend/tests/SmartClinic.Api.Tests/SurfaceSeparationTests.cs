using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using SmartClinic.Api.Endpoints;
using SmartClinic.Api.OpenApi;
using Xunit;

namespace SmartClinic.Api.Tests;

/// <summary>
/// T-005 acceptance evidence. Runs a real (in-process, no Docker needed)
/// minimal WebApplication using the SAME extension methods
/// (<see cref="OpenApiServiceCollectionExtensions"/>,
/// <see cref="SurfaceDocumentInclusion"/>) the real API uses, so these
/// tests exercise the actual mechanism, not a reimplementation of it.
/// </summary>
public sealed class SurfaceSeparationTests
{
    // ---- the real app's own endpoints ------------------------------------

    [Fact]
    public async Task Staff_and_portal_documents_are_disjoint()
    {
        await using var app = await BuildAppAsync(a => a.MapPipelineProbeEndpoints());

        var staff = await GetPathsAsync(app, SurfaceDocumentInclusion.StaffDocumentName);
        var portal = await GetPathsAsync(app, SurfaceDocumentInclusion.PortalDocumentName);

        var overlap = staff.Intersect(portal).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public async Task Portal_document_contains_no_staff_only_probe_endpoint()
    {
        await using var app = await BuildAppAsync(a => a.MapPipelineProbeEndpoints());

        var portal = await GetPathsAsync(app, SurfaceDocumentInclusion.PortalDocumentName);

        Assert.DoesNotContain("/api/v1/_t005-probe/staff-only", portal);
    }

    [Fact]
    public async Task Staff_document_contains_no_portal_only_probe_endpoint()
    {
        await using var app = await BuildAppAsync(a => a.MapPipelineProbeEndpoints());

        var staff = await GetPathsAsync(app, SurfaceDocumentInclusion.StaffDocumentName);

        Assert.DoesNotContain("/api/v1/portal/_t005-probe/portal-only", staff);
    }

    [Fact]
    public async Task Every_portal_document_path_starts_with_the_portal_prefix()
    {
        await using var app = await BuildAppAsync(a => a.MapPipelineProbeEndpoints());

        var portal = await GetPathsAsync(app, SurfaceDocumentInclusion.PortalDocumentName);

        Assert.NotEmpty(portal);
        Assert.All(portal, path => Assert.StartsWith("/api/v1/portal/", path));
    }

    // ---- the generic mechanism, proven against BRAND-NEW endpoints ------
    // (i.e. not the two shipped probes — this is the "no hard-coded
    // endpoint list" proof: nothing below is registered anywhere in the
    // Swagger configuration, only mapped as ordinary routes.)

    [Fact]
    public async Task A_brand_new_staff_endpoint_is_automatically_placed_by_path_alone()
    {
        await using var app = await BuildAppAsync(a =>
            a.MapGet("/api/v1/some-future-feature/ping", () => "pong"));

        var staff = await GetPathsAsync(app, SurfaceDocumentInclusion.StaffDocumentName);
        var portal = await GetPathsAsync(app, SurfaceDocumentInclusion.PortalDocumentName);

        Assert.Contains("/api/v1/some-future-feature/ping", staff);
        Assert.DoesNotContain("/api/v1/some-future-feature/ping", portal);
    }

    [Fact]
    public async Task A_brand_new_portal_endpoint_is_automatically_placed_by_path_alone()
    {
        await using var app = await BuildAppAsync(a =>
            a.MapGet("/api/v1/portal/some-future-feature/ping", () => "pong"));

        var staff = await GetPathsAsync(app, SurfaceDocumentInclusion.StaffDocumentName);
        var portal = await GetPathsAsync(app, SurfaceDocumentInclusion.PortalDocumentName);

        Assert.Contains("/api/v1/portal/some-future-feature/ping", portal);
        Assert.DoesNotContain("/api/v1/portal/some-future-feature/ping", staff);
    }

    [Fact]
    public async Task Explicit_group_name_overrides_path_based_inference()
    {
        // A staff-shaped path explicitly opted into the portal document.
        // Proves the mechanism is a real, overridable convention, not a
        // hard-coded path matcher pretending to be one.
        await using var app = await BuildAppAsync(a =>
            a.MapGet("/api/v1/opts-into-portal", () => "pong")
                .WithGroupName(SurfaceDocumentInclusion.PortalDocumentName));

        var staff = await GetPathsAsync(app, SurfaceDocumentInclusion.StaffDocumentName);
        var portal = await GetPathsAsync(app, SurfaceDocumentInclusion.PortalDocumentName);

        Assert.Contains("/api/v1/opts-into-portal", portal);
        Assert.DoesNotContain("/api/v1/opts-into-portal", staff);
    }

    // ---- helpers ----------------------------------------------------------

    private static async Task<WebApplication> BuildAppAsync(Action<WebApplication> mapEndpoints)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSmartClinicOpenApi();

        var app = builder.Build();
        mapEndpoints(app);

        await app.StartAsync();
        return app;
    }

    private static async Task<HashSet<string>> GetPathsAsync(WebApplication app, string documentName)
    {
        var provider = app.Services.GetRequiredService<ISwaggerProvider>();
        var document = await Task.Run(() => provider.GetSwagger(documentName));
        return document.Paths.Select(p => p.Key).ToHashSet();
    }
}
