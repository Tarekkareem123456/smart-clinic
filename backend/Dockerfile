# T-010 — container image for the API (the single deployable unit, ADR-001).
#
# Build context is the `backend/` directory:
#   docker build -f backend/Dockerfile -t smartclinic-api backend
#
# Scope notes:
#  - Docker is the locked deployment model (Backend Stack Q32); .NET 8 follows
#    the existing net8.0 repository target in Directory.Build.props (not a
#    newly locked architecture decision).
#  - The background worker runs inside this same deployable (Backend Stack Q20 /
#    ADR-010), so there is exactly one image here. Whether a separate worker
#    image is wanted is the open traceability note E4 and is NOT decided here.
#  - The Migrator is not packaged here; applying migrations is a deploy-time
#    step (backend/database/MIGRATIONS.md), not part of T-010.
#  - The image is only built and scanned by CI; nothing is pushed or deployed.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SmartClinic.Api/SmartClinic.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Official .NET 8 images define APP_UID (non-root); never run as root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartClinic.Api.dll"]