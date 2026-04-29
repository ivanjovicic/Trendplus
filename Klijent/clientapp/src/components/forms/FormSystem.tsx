import React, { useEffect, useMemo, useReducer, useRef, useState } from "react";
import type { LucideIcon } from "lucide-react";

type Tone = "neutral" | "success" | "warning";

export type FormStepState = "pending" | "complete" | "warning";

export type FormStep = {
  label: string;
  state: FormStepState;
};

export type ValidationChecklistItem = {
  label: string;
  valid: boolean;
};

export type EntitySearchItem = {
  id: string | number;
  title: string;
  meta?: string;
  value?: React.ReactNode;
};

export type LineItem<Row> = {
  id: string | number;
  title?: string;
  status?: "new" | "existing" | "error" | "ok";
  error?: string | null;
  data: Row;
};

export type LineItemsAction<Row> =
  | { type: "reset"; rows: Array<LineItem<Row>> }
  | { type: "add"; row: LineItem<Row> }
  | { type: "remove"; id: string | number }
  | { type: "patch"; id: string | number; patch: Partial<Row>; rowPatch?: Partial<Omit<LineItem<Row>, "data">> };

export function lineItemsReducer<Row>(
  rows: Array<LineItem<Row>>,
  action: LineItemsAction<Row>
): Array<LineItem<Row>> {
  switch (action.type) {
    case "reset":
      return action.rows;
    case "add":
      return [...rows, action.row];
    case "remove":
      return rows.filter((row) => row.id !== action.id);
    case "patch":
      return rows.map((row) =>
        row.id === action.id
          ? {
              ...row,
              ...action.rowPatch,
              data: { ...row.data, ...action.patch },
            }
          : row
      );
    default:
      return rows;
  }
}

export function useLineItems<Row>(initialRows: Array<LineItem<Row>>) {
  return useReducer(lineItemsReducer<Row>, initialRows);
}

export function FormPageShell({
  title,
  subtitle,
  icon: Icon,
  actions,
  children,
}: {
  title: string;
  subtitle?: string;
  icon?: LucideIcon;
  actions?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="form-page">
      <header className="form-page__header">
        <div>
          <div className="line-row__header">
            <h1 className="form-page__title">
              {Icon ? <Icon size={18} aria-hidden="true" /> : null}
              {title}
            </h1>
          </div>
          {subtitle ? <p className="form-page__subtitle">{subtitle}</p> : null}
        </div>
        {actions ? <div>{actions}</div> : null}
      </header>
      {children}
    </div>
  );
}

export function FormLayout({ main, aside }: { main: React.ReactNode; aside: React.ReactNode }) {
  return (
    <div className="form-layout">
      <main className="form-layout__main">{main}</main>
      <aside className="form-layout__aside">{aside}</aside>
    </div>
  );
}

export function FormSection({
  title,
  description,
  complete,
  warning,
  children,
  actions,
}: {
  title: string;
  description?: string;
  complete?: boolean;
  warning?: boolean;
  children: React.ReactNode;
  actions?: React.ReactNode;
}) {
  const status = warning ? "Proveri" : complete ? "Završeno" : "U toku";
  const statusClass = warning
    ? "form-section__status form-section__status--warning"
    : complete
      ? "form-section__status form-section__status--complete"
      : "form-section__status";

  return (
    <section className="form-section">
      <div className="form-section__header">
        <div>
          <h2 className="form-section__title">{title}</h2>
          {description ? <p className="form-section__description">{description}</p> : null}
        </div>
        <div className="line-row__header">
          {actions}
          <span className={statusClass}>{status}</span>
        </div>
      </div>
      {children}
    </section>
  );
}

export function FormField({
  label,
  required,
  helper,
  error,
  children,
}: {
  label: string;
  required?: boolean;
  helper?: string;
  error?: string | null;
  children: React.ReactNode;
}) {
  return (
    <label className="form-field">
      <span className="form-label">
        {label} {required ? <span className="form-required">*</span> : null}
      </span>
      {children}
      {error ? <span className="form-error">{error}</span> : helper ? <span className="form-helper">{helper}</span> : null}
    </label>
  );
}

export function ReadonlyField({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="readonly-field">
      <span className="readonly-field__label">{label}</span>
      <span className="readonly-field__value">{value}</span>
    </div>
  );
}

export function CalculatedField({
  label,
  value,
  tone = "neutral",
}: {
  label: string;
  value: React.ReactNode;
  tone?: Tone;
}) {
  return (
    <div className={`calculated-field calculated-field--${tone}`}>
      <span className="calculated-field__label">{label}</span>
      <span className="calculated-field__value">{value}</span>
    </div>
  );
}

export function SummaryPanel({
  title = "Pregled",
  children,
  actions,
}: {
  title?: string;
  children: React.ReactNode;
  actions?: React.ReactNode;
}) {
  return (
    <section className="summary-panel">
      <h2 className="summary-panel__title">{title}</h2>
      {children}
      {actions ? <div className="summary-panel__actions">{actions}</div> : null}
    </section>
  );
}

export function ValidationChecklist({ items }: { items: ValidationChecklistItem[] }) {
  return (
    <div className="validation-list">
      <span className="validation-list__label">Validacija</span>
      {items.map((item) => (
        <span
          key={item.label}
          className={`validation-list__item ${item.valid ? "validation-list__item--valid" : "validation-list__item--invalid"}`}
        >
          <span aria-hidden="true">{item.valid ? "✓" : "!"}</span>
          {item.label}
        </span>
      ))}
    </div>
  );
}

export function FormProgress({ steps }: { steps: FormStep[] }) {
  return (
    <nav className="form-progress" aria-label="Tok forme">
      {steps.map((step, index) => (
        <div key={`${step.label}-${index}`} className={`form-step form-step--${step.state}`}>
          <span className="form-step__index">{index + 1}</span>
          <span className="form-step__label">{step.label}</span>
        </div>
      ))}
    </nav>
  );
}

export function StickyActionBar({
  primaryLabel,
  disabled,
  disabledReason,
  onPrimary,
}: {
  primaryLabel: string;
  disabled?: boolean;
  disabledReason?: string;
  onPrimary: () => void;
}) {
  return (
    <div className="sticky-action-bar">
      {disabled && disabledReason ? <span className="form-helper">{disabledReason}</span> : null}
      <button type="button" className="btn btn--primary btn--full" disabled={disabled} onClick={onPrimary}>
        {primaryLabel}
      </button>
    </div>
  );
}

export function EntitySearchCombobox({
  label,
  value,
  placeholder,
  items,
  loading,
  required,
  helper,
  emptyText = "Nema rezultata.",
  onQueryChange,
  onSelect,
}: {
  label: string;
  value: string;
  placeholder?: string;
  items: EntitySearchItem[];
  loading?: boolean;
  required?: boolean;
  helper?: string;
  emptyText?: string;
  onQueryChange: (value: string) => void;
  onSelect: (item: EntitySearchItem) => void;
}) {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const rootRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    setActiveIndex(0);
  }, [items.length]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const showResults = open && value.trim().length > 0;

  return (
    <div className="entity-search" ref={rootRef}>
      <FormField label={label} required={required} helper={helper}>
        <input
          className="form-control"
          type="text"
          value={value}
          placeholder={placeholder}
          onChange={(event) => {
            onQueryChange(event.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={(event) => {
            if (!showResults || items.length === 0) return;
            if (event.key === "ArrowDown") {
              event.preventDefault();
              setActiveIndex((current) => (current + 1) % items.length);
            }
            if (event.key === "ArrowUp") {
              event.preventDefault();
              setActiveIndex((current) => (current - 1 + items.length) % items.length);
            }
            if (event.key === "Enter") {
              event.preventDefault();
              onSelect(items[activeIndex]);
              setOpen(false);
            }
            if (event.key === "Escape") setOpen(false);
          }}
        />
      </FormField>
      {showResults ? (
        <div className="entity-search__results">
          {loading ? (
            <div className="form-note">Pretraga u toku...</div>
          ) : items.length > 0 ? (
            items.map((item, index) => (
              <button
                key={item.id}
                type="button"
                className={`entity-search__item ${index === activeIndex ? "entity-search__item--active" : ""}`}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => {
                  onSelect(item);
                  setOpen(false);
                }}
              >
                <span>
                  <span className="entity-search__item-title">{item.title}</span>
                  {item.meta ? <span className="entity-search__item-meta">{item.meta}</span> : null}
                </span>
                {item.value ? <span>{item.value}</span> : null}
              </button>
            ))
          ) : (
            <div className="form-note">{emptyText}</div>
          )}
        </div>
      ) : null}
    </div>
  );
}

function LineItemsRow<Row>({
  row,
  children,
  grid,
}: {
  row: LineItem<Row>;
  children: React.ReactNode;
  grid?: "sale" | "receive" | "return" | "transfer";
}) {
  const status = row.status ?? "ok";
  return (
    <article className={`line-row ${status === "error" ? "line-row--error" : ""}`}>
      <div className="line-row__header">
        <span className="line-row__title">{row.title ?? `Stavka ${row.id}`}</span>
        <span className={`row-status row-status--${status}`}>
          {status === "existing" ? "Postojeći" : status === "new" ? "Novi" : status === "error" ? "Greška" : "OK"}
        </span>
      </div>
      <div className={`line-row__grid ${grid ? `line-row__grid--${grid}` : ""}`}>{children}</div>
      {row.error ? <p className="form-error">{row.error}</p> : null}
    </article>
  );
}

const MemoLineItemsRow = React.memo(LineItemsRow) as typeof LineItemsRow;

export function LineItemsEditor<Row>({
  title,
  rows,
  grid,
  onAdd,
  addLabel = "Dodaj stavku",
  renderRow,
}: {
  title: string;
  rows: Array<LineItem<Row>>;
  grid?: "sale" | "receive" | "return" | "transfer";
  onAdd?: () => void;
  addLabel?: string;
  renderRow: (row: LineItem<Row>, index: number) => React.ReactNode;
}) {
  const renderedRows = useMemo(
    () =>
      rows.map((row, index) => (
        <MemoLineItemsRow key={row.id} row={row} grid={grid}>
          {renderRow(row, index)}
        </MemoLineItemsRow>
      )),
    [grid, renderRow, rows]
  );

  return (
    <div className="line-items">
      <div className="line-items__head">
        <h3 className="form-section__title">{title}</h3>
        {onAdd ? (
          <button type="button" className="btn btn--secondary" onClick={onAdd}>
            {addLabel}
          </button>
        ) : null}
      </div>
      <div className="line-items__list">
        {rows.length > 0 ? renderedRows : <div className="form-note">Dodajte prvu stavku.</div>}
      </div>
    </div>
  );
}
