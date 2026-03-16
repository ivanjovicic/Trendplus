import type { Sezona } from "../../types/Sezona";

export type SupplierDecisionFilterFormState = {
  fromDate: string;
  toDate: string;
  category: string;
  gender: string;
  seasonId: string;
  minRevenue: string;
  onlyHighConfidence: boolean;
  excludeOosBeforeMarkdown: boolean;
};

type SupplierDecisionFiltersProps = {
  value: SupplierDecisionFilterFormState;
  seasons: Sezona[];
  pending?: boolean;
  onChange: (next: SupplierDecisionFilterFormState) => void;
  onApply: () => void;
  onReset: () => void;
};

const genderOptions = [
  { value: "", label: "Svi" },
  { value: "Žensko", label: "Žensko" },
  { value: "Muško", label: "Muško" },
  { value: "Unisex", label: "Unisex" },
  { value: "Dečije", label: "Dečije" },
];

export default function SupplierDecisionFilters({
  value,
  seasons,
  pending = false,
  onChange,
  onApply,
  onReset,
}: SupplierDecisionFiltersProps) {
  const updateField = <K extends keyof SupplierDecisionFilterFormState>(
    field: K,
    nextValue: SupplierDecisionFilterFormState[K]
  ) => {
    onChange({
      ...value,
      [field]: nextValue,
    });
  };

  return (
    <form
      className="supplier-decision-filters"
      onSubmit={(event) => {
        event.preventDefault();
        onApply();
      }}
    >
      <label className="supplier-decision-field supplier-decision-field-period">
        <span>Period</span>
        <div className="supplier-decision-period-grid">
          <input
            type="date"
            value={value.fromDate}
            max={value.toDate || undefined}
            onChange={(event) => updateField("fromDate", event.target.value)}
          />
          <input
            type="date"
            value={value.toDate}
            min={value.fromDate || undefined}
            onChange={(event) => updateField("toDate", event.target.value)}
          />
        </div>
      </label>

      <label className="supplier-decision-field">
        <span>Kategorija</span>
        <input
          type="text"
          value={value.category}
          placeholder="npr. Patike"
          onChange={(event) => updateField("category", event.target.value)}
        />
      </label>

      <label className="supplier-decision-field">
        <span>Pol</span>
        <select
          value={value.gender}
          onChange={(event) => updateField("gender", event.target.value)}
        >
          {genderOptions.map((option) => (
            <option key={option.value || "all"} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </label>

      <label className="supplier-decision-field">
        <span>Sezona</span>
        <select
          value={value.seasonId}
          onChange={(event) => updateField("seasonId", event.target.value)}
        >
          <option value="">Sve sezone</option>
          {seasons.map((season) => (
            <option key={season.id} value={season.id}>
              {season.naziv}
            </option>
          ))}
        </select>
      </label>

      <label className="supplier-decision-field">
        <span>Minimalni prihod</span>
        <input
          type="number"
          min="0"
          step="1000"
          value={value.minRevenue}
          placeholder="0"
          onChange={(event) => updateField("minRevenue", event.target.value)}
        />
      </label>

      <label className="supplier-decision-check">
        <input
          type="checkbox"
          checked={value.onlyHighConfidence}
          onChange={(event) => updateField("onlyHighConfidence", event.target.checked)}
        />
        <span>Samo visoka pouzdanost</span>
      </label>

      <label className="supplier-decision-check">
        <input
          type="checkbox"
          checked={value.excludeOosBeforeMarkdown}
          onChange={(event) => updateField("excludeOosBeforeMarkdown", event.target.checked)}
        />
        <span>Isključi artikle bez zaliha pre sniženja</span>
      </label>

      <div className="supplier-decision-filter-actions">
        <button type="submit" disabled={pending}>
          {pending ? "Učitavanje..." : "Primeni filtere"}
        </button>
        <button
          type="button"
          className="secondary"
          onClick={onReset}
          disabled={pending}
        >
          Resetuj
        </button>
      </div>
    </form>
  );
}
