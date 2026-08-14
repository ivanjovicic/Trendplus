export function assertHonestDecisionTimelineExportCsv(csv: string): string {
  const normalized = csv?.trim() ?? "";
  if (!normalized || /^# success=false/m.test(normalized)) {
    throw new Error("Decision Timeline export trenutno nije dostupan.");
  }

  return csv;
}

export function downloadDecisionTimelineExportCsv(filename: string, csv: string): void {
  const honestCsv = assertHonestDecisionTimelineExportCsv(csv);
  const blob = new Blob([honestCsv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
