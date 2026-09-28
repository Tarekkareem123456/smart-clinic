using Microsoft.OpenApi.Models;

namespace SmartClinic.Api.OpenApi;

/// <summary>
/// T-005 — wires Swashbuckle to produce exactly two OpenAPI documents,
/// "staff" and "portal", split purely by <see cref="SurfaceDocumentInclusion"/>.
/// No business/domain schema is registered here.
/// </summary>
public static class OpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddSmartClinicOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(SurfaceDocumentInclusion.StaffDocumentName, new OpenApiInfo
            {
                Title = "Smart Clinic — Staff API",
                Version = "v1",
            });

            options.SwaggerDoc(SurfaceDocumentInclusion.PortalDocumentName, new OpenApiInfo
            {
                Title = "Smart Clinic — Patient Portal API",
                Version = "v1",
            });

            options.DocInclusionPredicate((documentName, apiDescription) =>
                SurfaceDocumentInclusion.BelongsToDocument(
                    documentName,
                    apiDescription.GroupName,
                    apiDescription.RelativePath));
        });

        return services;
    }

    /// <summary>Exposes /swagger/staff/swagger.json and /swagger/portal/swagger.json. No SwaggerUI (not required by any T-005 AC; keeps the surface minimal).</summary>
    public static WebApplication UseSmartClinicOpenApi(this WebApplication app)
    {
        app.UseSwagger();
        return app;
    }
}
