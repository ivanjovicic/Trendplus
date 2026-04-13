import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Zap } from "lucide-react";
import {
  runNivelacijaRepairPreflight,
  runNivelacijaRepairDryRun,
  type NivelacijaRepairPreflightDto,
  type NivelacijaRepairDryRunResponse,
} from "../services/nivelacijaRepairApi";
import "./NivelacijaRepairPage.css";

type Tab = "preflight" | "dry-run";

interface DryRunState {
  loading: boolean;
  error: string | null;
  result: NivelacijaRepairDryRunResponse | null;
}

export default function NivelacijaRepairPage() {
  const [tab, setTab] = useState<Tab>("preflight");
  const [adminKey, setAdminKey] = useState("");
  const [sourceFilePath, setSourceFilePath] = useState("");
  const [maxRowsToModify, setMaxRowsToModify] = useState(10000);

  const [preflightLoading, setPreflightLoading] = useState(false);
  const [preflightError, setPreflightError] = useState<string | null>(null);
  const [preflight, setPreflight] = useState<NivelacijaRepairPreflightDto | null>(null);

  const [dryRun, setDryRun] = useState<DryRunState>({
    loading: false,
    error: null,
    result: null,
  });

  const handlePreflight = async () => {
    setPreflightLoading(true);
    setPreflightError(null);
    try {
      const result = await runNivelacijaRepairPreflight(
        sourceFilePath || undefined,
        adminKey || undefined
      );
      setPreflight(result);
      setTab("dry-run");
    } catch (err) {
      setPreflightError(err instanceof Error ? err.message : "Unknown error");
    } finally {
      setPreflightLoading(false);
    }
  };

  const handleDryRun = async () => {
    if (!preflight) return;
    setDryRun({ loading: true, error: null, result: null });
    try {
      const result = await runNivelacijaRepairDryRun(
        sourceFilePath || preflight.resolvedSourceFilePath,
        maxRowsToModify,
        adminKey || undefined
      );
      setDryRun({ loading: false, error: null, result });
    } catch (err) {
      setDryRun({
        loading: false,
        error: err instanceof Error ? err.message : "Unknown error",
        result: null,
      });
    }
  };

  const downloadJSON = () => {
    if (!dryRun.result) return;
    const json = JSON.stringify(dryRun.result, null, 2);
    const blob = new Blob([json], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `nivelacija_dry_run_${new Date().toISOString().replace(/[:.]/g, "-")}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  const canExecuteDryRun = preflight && !dryRun.loading;

  return (
    <div className="nivelacija-repair-page">
      <header className="nivelacija-repair-header">
        <h1>Nivelacija Repair — Preflight i Dry Run</h1>
        <p>Provera i simulacija popravki za nivelacija linije redove</p>
      </header>

      <div className="nivelacija-repair-tabs">
        <button
          className={`tab-button ${tab === "preflight" ? "active" : ""}`}
          onClick={() => setTab("preflight")}
        >
          Preflight
        </button>
        <button
          className={`tab-button ${tab === "dry-run" ? "active" : ""}`}
          onClick={() => setTab("dry-run")}
          disabled={!preflight}
        >
          Dry Run
        </button>
      </div>

      {tab === "preflight" && (
        <section className="nivelacija-repair-section">
          <h2>Korak 1: Preflight Provera</h2>
          <div className="nivelacija-repair-form">
            <div className="form-group">
              <label>Admin Key (X-Admin-Key)</label>
              <input
                type="password"
                placeholder="Unesite admin ključ..."
                value={adminKey}
                onChange={(e) => setAdminKey(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label>Source File Path (opciono)</label>
              <input
                type="text"
                placeholder="C:\\data\\Trend plus.mdb"
                value={sourceFilePath}
                onChange={(e) => setSourceFilePath(e.target.value)}
              />
              <small>Ako je prazno, backend koristi default putanju</small>
            </div>

            <button
              className="btn btn-primary"
              onClick={handlePreflight}
              disabled={preflightLoading}
            >
              {preflightLoading ? "Proverava..." : "Pokreni Preflight"}
            </button>

            {preflightError && (
              <div className="alert alert-error">
                <AlertTriangle size={16} />
                {preflightError}
              </div>
            )}

            {preflight && (
              <div className="preflight-results">
                <h3>Rezultati Preflight-a</h3>

                <div className={`status-box ${preflight.databaseReachable ? "success" : "error"}`}>
                  {preflight.databaseReachable ? (
                    <CheckCircle2 size={20} />
                  ) : (
                    <AlertTriangle size={20} />
                  )}
                  <span>
                    Baza podataka:{" "}
                    {preflight.databaseReachable ? "Dostupna" : "Nije dostupna"}
                  </span>
                </div>

                <div className="info-box">
                  <strong>Resolved Source File:</strong> {preflight.resolvedSourceFilePath}
                </div>

                <div className="info-box">
                  <strong>Default Threshold:</strong> {preflight.defaultMaxRowsThreshold} redova
                </div>

                <div className="info-box">
                  <strong>Required Objects:</strong>
                  <ul>
                    {Object.entries(preflight.requiredObjects).map(([key, available]) => (
                      <li key={key} className={available ? "available" : "missing"}>
                        {key}: {available ? "✓" : "✗"}
                      </li>
                    ))}
                  </ul>
                </div>

                <div className="info-box">
                  <strong>Access Tables:</strong>
                  <ul>
                    {Object.entries(preflight.accessTables).map(([key, tableName]) => (
                      <li key={key}>
                        {key}: <code>{tableName || "—"}</code>
                      </li>
                    ))}
                  </ul>
                </div>

                {preflight.warnings.length > 0 && (
                  <div className="alert alert-warning">
                    <strong>Upozorenja:</strong>
                    <ul>
                      {preflight.warnings.map((w, i) => (
                        <li key={i}>{w}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </div>
        </section>
      )}

      {tab === "dry-run" && preflight && (
        <section className="nivelacija-repair-section">
          <h2>Korak 2: Dry Run (Simulacija)</h2>
          <div className="nivelacija-repair-form">
            <div className="form-group">
              <label>Max Rows to Modify</label>
              <input
                type="number"
                min={1}
                max={100000}
                value={maxRowsToModify}
                onChange={(e) => setMaxRowsToModify(Number(e.target.value))}
              />
              <small>Safety threshold — dry-run biće odbijen ako premaši ovu vrednost</small>
            </div>

            <button
              className="btn btn-primary"
              onClick={handleDryRun}
              disabled={!canExecuteDryRun}
            >
              {dryRun.loading ? "Pokreće se..." : "Pokreni Dry Run"}
            </button>

            {dryRun.error && (
              <div className="alert alert-error">
                <AlertTriangle size={16} />
                {dryRun.error}
              </div>
            )}

            {dryRun.result && (
              <div className="dry-run-results">
                <h3>Rezultati Dry Run-a</h3>

                <div className="results-summary">
                  <div className="summary-card">
                    <span className="label">Candidate Rows Scanned</span>
                    <span className="value">
                      {dryRun.result.estimatedImpact.candidateRowsScanned}
                    </span>
                  </div>
                  <div className="summary-card">
                    <span className="label">Detected Issues</span>
                    <span className="value highlight">
                      {dryRun.result.estimatedImpact.detectedIssuesCount}
                    </span>
                  </div>
                  <div className="summary-card">
                    <span className="label">Proposed Fixes</span>
                    <span className="value">
                      {dryRun.result.estimatedImpact.proposedFixesCount}
                    </span>
                  </div>
                  <div className="summary-card">
                    <span className="label">Threshold</span>
                    <span className="value">
                      {dryRun.result.estimatedImpact.maxRowsThreshold}
                    </span>
                  </div>
                </div>

                <div
                  className={`impact-box ${
                    dryRun.result.estimatedImpact.canExecute ? "success" : "error"
                  }`}
                >
                  <strong>Can Execute:</strong> {dryRun.result.estimatedImpact.canExecute ? "✓ Da" : "✗ Ne"}
                  {!dryRun.result.estimatedImpact.canExecute && (
                    <p className="note">
                      Predloženi fixes ({dryRun.result.estimatedImpact.proposedFixesCount}) prate
                      safety threshold ({dryRun.result.estimatedImpact.maxRowsThreshold}).
                    </p>
                  )}
                </div>

                <div className="info-box">
                  <strong>Field Changes:</strong>
                  <ul>
                    <li>Updated date rows: {dryRun.result.estimatedImpact.updatedDateRows}</li>
                    <li>Updated store rows: {dryRun.result.estimatedImpact.updatedStoreRows}</li>
                    <li>Updated vendor rows: {dryRun.result.estimatedImpact.updatedVendorRows}</li>
                  </ul>
                </div>

                <div className="info-box">
                  <strong>Verification Checks:</strong>
                  <ul className="verification-checks">
                    <li className={dryRun.result.verification.aggregate.accessLinesMatchVendorRows ? "pass" : "fail"}>
                      Access lines == vendor rows
                    </li>
                    <li className={dryRun.result.verification.aggregate.accessEventsMatchImportedSourceHeaders ? "pass" : "fail"}>
                      Access events == imported source headers
                    </li>
                    <li>Imported duplicate groups: {dryRun.result.verification.edgeCases.importedDuplicateGroups}</li>
                    <li>Multiple changes same day: {dryRun.result.verification.edgeCases.multipleChangesSameDayRows}</li>
                  </ul>
                </div>

                <div className="audit-info">
                  <strong>Audit ID:</strong> {dryRun.result.auditId}
                </div>

                <button className="btn btn-secondary" onClick={downloadJSON}>
                  <Zap size={16} /> Preuzmi JSON Rezultat
                </button>

                <div className="note-box">
                  <strong>Sledeći korak:</strong> Ako su rezultati zadovoljavajući, koristi
                  runbook (repair_nivelacije.ps1) ili GitHub Actions workflow za live execution sa
                  confirm=true.
                </div>
              </div>
            )}
          </div>
        </section>
      )}
    </div>
  );
}
