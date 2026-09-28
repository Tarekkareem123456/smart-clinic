// T-005 (TR-11) — this is the only place in apps/portal allowed to import
// the generated Portal client (enforced by scripts/check-client-usage.mjs).
// Hooks/features call this service; they never import ../generated/*
// directly.
import type { PortalApiPaths } from "../generated/portal-client";

export async function pingPortalOnlyProbe(): Promise<{ surface: "portal" }> {
  // Placeholder call against the T-005 pipeline-verification endpoint
  // (backend/src/SmartClinic.Api/Endpoints/PipelineProbeEndpoints.cs).
  // Not business functionality.
  const response = await fetch("/api/v1/portal/_t005-probe/portal-only");
  return (await response.json()) as { surface: "portal" };
}

// Referenced so the (currently empty) placeholder type is not unused.
export type _PortalApiPathsRef = PortalApiPaths;
