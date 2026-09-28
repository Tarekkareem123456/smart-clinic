// T-005 (TR-11) — this is the only place in apps/staff allowed to import
// the generated Staff client (enforced by scripts/check-client-usage.mjs).
// Hooks/features call this service; they never import ../generated/*
// directly.
import type { StaffApiPaths } from "../generated/staff-client";

export async function pingStaffOnlyProbe(): Promise<{ surface: "staff" }> {
  // Placeholder call against the T-005 pipeline-verification endpoint
  // (backend/src/SmartClinic.Api/Endpoints/PipelineProbeEndpoints.cs).
  // Not business functionality.
  const response = await fetch("/api/v1/_t005-probe/staff-only");
  return (await response.json()) as { surface: "staff" };
}

// Referenced so the (currently empty) placeholder type is not unused.
export type _StaffApiPathsRef = StaffApiPaths;
