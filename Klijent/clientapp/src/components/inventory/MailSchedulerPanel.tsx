import { Mail, Play, RefreshCw } from "lucide-react";
import type { InventoryReportSchedule, InventoryReportScheduleInput } from "../../types/analytics";
import { WEEKDAY_OPTIONS } from "./inventoryUtils";

type MailSchedulerPanelProps = {
  scheduleDraft: InventoryReportScheduleInput;
  setScheduleDraft: React.Dispatch<React.SetStateAction<InventoryReportScheduleInput>>;
  schedules: InventoryReportSchedule[];
  schedulerBusy: boolean;
  schedulerMessage: string | null;
  onCopyCurrentFilters: () => void;
  onSaveSchedule: () => void;
  onRunScheduleNow: (id: number) => void;
};

export function MailSchedulerPanel({
  scheduleDraft,
  setScheduleDraft,
  schedules,
  schedulerBusy,
  schedulerMessage,
  onCopyCurrentFilters,
  onSaveSchedule,
  onRunScheduleNow,
}: MailSchedulerPanelProps) {
  return (
    <section className="grid gap-5 xl:grid-cols-[1.1fr_0.9fr]">
      <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-white">Mail scheduler za dnevni i nedeljni report</h2>
            <p className="text-sm text-[#90a0ba]">Zakazi PDF/Excel/CSV bilans stanja, sa lokalnim vremenom, filterima i rucnim pokretanjem.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={onCopyCurrentFilters} className="inline-flex items-center gap-2 rounded-xl border border-[#30516d] bg-[#102231] px-3 py-2 text-xs font-semibold text-[#8edbff]">
              <RefreshCw size={14} />
              Preuzmi trenutne filtere
            </button>
            <button type="button" onClick={onSaveSchedule} disabled={schedulerBusy || !scheduleDraft.name.trim() || !scheduleDraft.recipientsCsv.trim()} className="inline-flex items-center gap-2 rounded-xl border border-[#36543f] bg-[#17261d] px-3 py-2 text-xs font-semibold text-[#aef3bf] disabled:cursor-not-allowed disabled:opacity-60">
              <Mail size={14} />
              Sacuvaj raspored
            </button>
          </div>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Naziv rasporeda</span>
            <input value={scheduleDraft.name} onChange={(event) => setScheduleDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Npr. Nedeljni retail PDF" className="w-full bg-transparent outline-none placeholder:text-[#73809a]" />
          </label>
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Primaoci</span>
            <input value={scheduleDraft.recipientsCsv} onChange={(event) => setScheduleDraft((current) => ({ ...current, recipientsCsv: event.target.value }))} placeholder="manager@firma.rs; retail@firma.rs" className="w-full bg-transparent outline-none placeholder:text-[#73809a]" />
          </label>
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Frekvencija</span>
            <select value={scheduleDraft.frequency} onChange={(event) => setScheduleDraft((current) => ({ ...current, frequency: event.target.value as "daily" | "weekly" }))} className="w-full bg-transparent outline-none">
              <option value="daily">Dnevno</option>
              <option value="weekly">Nedeljno</option>
            </select>
          </label>
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Dan / vreme</span>
            <div className="grid grid-cols-[1fr_auto] gap-2">
              <select value={scheduleDraft.dayOfWeek ?? 1} onChange={(event) => setScheduleDraft((current) => ({ ...current, dayOfWeek: Number(event.target.value) }))} className="w-full bg-transparent outline-none">
                {WEEKDAY_OPTIONS.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
              <input type="time" value={scheduleDraft.runAtLocalTime} onChange={(event) => setScheduleDraft((current) => ({ ...current, runAtLocalTime: event.target.value }))} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-[#dbe6fb] outline-none" />
            </div>
          </label>
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Format</span>
            <select value={scheduleDraft.format} onChange={(event) => setScheduleDraft((current) => ({ ...current, format: event.target.value as "pdf" | "xlsx" | "csv" }))} className="w-full bg-transparent outline-none">
              <option value="pdf">PDF</option>
              <option value="xlsx">Excel</option>
              <option value="csv">CSV</option>
            </select>
          </label>
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Predmet mejla</span>
            <input value={scheduleDraft.subject ?? ""} onChange={(event) => setScheduleDraft((current) => ({ ...current, subject: event.target.value }))} placeholder="Bilans stanja | dnevni pregled" className="w-full bg-transparent outline-none placeholder:text-[#73809a]" />
          </label>
          <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Time zone</span>
            <input value={scheduleDraft.timeZoneId} onChange={(event) => setScheduleDraft((current) => ({ ...current, timeZoneId: event.target.value }))} className="w-full bg-transparent outline-none" />
          </label>
          <label className="flex items-center gap-3 rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
            <input type="checkbox" checked={scheduleDraft.isEnabled} onChange={(event) => setScheduleDraft((current) => ({ ...current, isEnabled: event.target.checked }))} />
            <span>Raspored je aktivan odmah po cuvanju</span>
          </label>
        </div>

        {schedulerMessage ? <div className="mt-4 rounded-2xl border border-[#284058] bg-[#101a24] px-4 py-3 text-sm text-[#9edcff]">{schedulerMessage}</div> : null}

        <div className="mt-5 space-y-3">
          {schedules.length === 0 ? <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Jos nema sacuvanih rasporeda za Bilans stanja.</div> : schedules.map((schedule) => (
            <div key={schedule.id} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
              <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-semibold text-white">{schedule.name}</span>
                    <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${schedule.isEnabled ? "border-[#28574d] bg-[#102b24] text-[#9ff0c7]" : "border-[#6b2c38] bg-[#281319] text-[#ffc3cf]"}`}>{schedule.isEnabled ? "Aktivan" : "Pauziran"}</span>
                    <span className="inline-flex rounded-full border border-[#33405a] bg-[#182131] px-2.5 py-1 text-[11px] font-semibold text-[#dbe6fb]">{schedule.frequency === "weekly" ? "Nedeljno" : "Dnevno"} u {schedule.runAtLocalTime}</span>
                  </div>
                  <div className="mt-2 text-sm text-[#90a0ba]">{schedule.format.toUpperCase()} | {schedule.recipientsCsv}</div>
                  <div className="mt-2 text-xs text-[#7f8fa9]">
                    Poslednje pokretanje: {schedule.lastRunAtUtc ? new Date(schedule.lastRunAtUtc).toLocaleString("sr-RS") : "jos nije pokrenuto"}{schedule.lastRunStatus ? ` | status: ${schedule.lastRunStatus}` : ""}
                  </div>
                  {schedule.lastError ? <div className="mt-2 text-xs text-[#ffbdcb]">{schedule.lastError}</div> : null}
                </div>
                <button type="button" onClick={() => onRunScheduleNow(schedule.id)} disabled={schedulerBusy} className="inline-flex items-center gap-2 rounded-xl border border-[#30516d] bg-[#102231] px-3 py-2 text-xs font-semibold text-[#8edbff] disabled:cursor-not-allowed disabled:opacity-60">
                  <Play size={14} />
                  Pokreni sada
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex items-start gap-3">
          <div className="rounded-2xl border border-[#30516d] bg-[#102231] p-3 text-[#8edbff]"><Mail size={18} /></div>
          <div>
            <h2 className="text-lg font-semibold text-white">Sta scheduler sada pokriva</h2>
            <p className="mt-2 text-sm leading-6 text-[#90a0ba]">Scheduler koristi isti server-side export kao rucni PDF/Excel, pa menadzment dobija isti izgled i iste filtere kao operativa na ekranu.</p>
          </div>
        </div>
        <div className="mt-5 grid gap-3">
          {[
            "Dnevni ili nedeljni PDF/Excel/CSV report za izabrani store, dobavljaca ili pretragu.",
            "Rucni 'run now' za proveru pre nego sto raspored pustis timu.",
            "Fail-safe ponasanje: ako SMTP nije ukljucen, dokument se i dalje generise i scheduler ne pada.",
            "Subject, filter scope i lokalno vreme se cuvaju uz svaki raspored.",
          ].map((line) => (
            <div key={line} className="rounded-2xl border border-[#243040] bg-[#10141b] px-4 py-3 text-sm text-[#dbe6fb]">{line}</div>
          ))}
        </div>
      </div>
    </section>
  );
}
