import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { apiUrl } from "../utils/apiUrl";

export interface NivelacijaRepairPreflightDto {
  resolvedSourceFilePath: string;
  databaseReachable: boolean;
  defaultMaxRowsThreshold: number;
  requiredObjects: Record<string, boolean>;
  accessTables: Record<string, string>;
  warnings: string[];
}

export interface NivelacijaRepairEstimatedImpactDto {
  candidateRowsScanned: number;
  detectedIssuesCount: number;
  proposedFixesCount: number;
  missingSourceMappings: number;
  updatedDateRows: number;
  updatedStoreRows: number;
  updatedVendorRows: number;
  maxRowsThreshold: number;
  exceedsThreshold: boolean;
  canExecute: boolean;
}

export interface NivelacijaRepairVerificationDto {
  aggregate: {
    accessLinesMatchVendorRows: boolean;
    preQtyMatchesVendorQty: boolean;
    preRevenueMatchesVendorRevenue: boolean;
    postQtyMatchesVendorQty: boolean;
    postRevenueMatchesVendorRevenue: boolean;
    accessEventsMatchImportedSourceHeaders: boolean;
  };
  edgeCases: {
    importedDuplicateGroups: number;
    viewDuplicateGroups: number;
    zeroSalesPeriodRows: number;
    inactiveRows: number;
    multipleChangesSameDayRows: number;
  };
}

export interface NivelacijaRepairDryRunResponse {
  estimatedImpact: NivelacijaRepairEstimatedImpactDto;
  verification: NivelacijaRepairVerificationDto;
  auditId: number;
  sourceFilePath: string;
}

async function parseError(res: Response): Promise<string> {
  try {
    const json = await res.json();
    return json.message || res.statusText;
  } catch {
    return res.statusText;
  }
}

export async function runNivelacijaRepairPreflight(
  sourceFilePath?: string,
  adminKey?: string
): Promise<NivelacijaRepairPreflightDto> {
  const url = sourceFilePath
    ? `${apiUrl("/admin/repair/nivelacije/preflight")}?sourceFilePath=${encodeURIComponent(sourceFilePath)}`
    : apiUrl("/admin/repair/nivelacije/preflight");

  const headers: Record<string, string> = {};
  if (adminKey) {
    headers["X-Admin-Key"] = adminKey;
  }

  const res = await fetchWithTimeout(url, { headers }, 20_000);
  if (!res.ok) {
    throw new Error(
      `Preflight failed: ${res.status} ${await parseError(res)}`
    );
  }
  return res.json();
}

export async function runNivelacijaRepairDryRun(
  sourceFilePath: string,
  maxRowsToModify: number = 10000,
  adminKey?: string
): Promise<NivelacijaRepairDryRunResponse> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  if (adminKey) {
    headers["X-Admin-Key"] = adminKey;
  }

  const body = JSON.stringify({
    dryRun: true,
    confirm: false,
    sourceFilePath,
    maxRowsToModify,
  });

  const res = await fetchWithTimeout(
    apiUrl("/admin/repair/nivelacije"),
    {
      method: "POST",
      headers,
      body,
    },
    20_000
  );

  if (!res.ok) {
    throw new Error(
      `Dry run failed: ${res.status} ${await parseError(res)}`
    );
  }
  return res.json();
}
