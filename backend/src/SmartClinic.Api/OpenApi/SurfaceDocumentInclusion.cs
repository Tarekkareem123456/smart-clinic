namespace SmartClinic.Api.OpenApi;

/// <summary>
/// T-005 — the one rule that decides which OpenAPI document ("staff" or
/// "portal") an endpoint belongs to. This IS the whole separation
/// mechanism: there is no hand-maintained list of endpoint names anywhere
/// in this codebase. Wired into Swashbuckle via
/// <c>SwaggerGenOptions.DocInclusionPredicate</c> in
/// <see cref="OpenApiServiceCollectionExtensions"/>.
///
/// Rule (matches the already-frozen Architecture route table — every
/// route under <c>/api/v1/portal/</c> is the Portal-scoped mirror;
/// everything else under <c>/api/v1/</c> is Staff):
///
///  1. If the endpoint has an EXPLICIT group name — set via
///     <c>[ApiExplorerSettings(GroupName = "portal")]</c> on a controller,
///     or <c>.WithGroupName("portal")</c> on a minimal-API route — that
///     value wins.
///  2. Otherwise the group is inferred purely from the route: "portal" if
///     the relative path starts with "api/v1/portal/", "staff" for
///     everything else.
///
/// A brand-new endpoint therefore lands in exactly one document purely by
/// satisfying rule 1 or rule 2 above — nothing else needs to be
/// configured or updated for it to be picked up correctly.
/// </summary>
public static class SurfaceDocumentInclusion
{
    public const string StaffDocumentName = "staff";
    public const string PortalDocumentName = "portal";

    private const string PortalRoutePrefix = "api/v1/portal/";

    /// <summary>Resolves the single group ("staff" or "portal") an endpoint belongs to.</summary>
    public static string ResolveGroup(string? explicitGroupName, string? relativePath)
    {
        if (!string.IsNullOrEmpty(explicitGroupName))
        {
            return explicitGroupName;
        }

        var path = (relativePath ?? string.Empty).TrimStart('/');
        return path.StartsWith(PortalRoutePrefix, StringComparison.OrdinalIgnoreCase)
            ? PortalDocumentName
            : StaffDocumentName;
    }

    /// <summary>Used directly as Swashbuckle's <c>DocInclusionPredicate</c>.</summary>
    public static bool BelongsToDocument(string documentName, string? explicitGroupName, string? relativePath) =>
        string.Equals(ResolveGroup(explicitGroupName, relativePath), documentName, StringComparison.Ordinal);
}
