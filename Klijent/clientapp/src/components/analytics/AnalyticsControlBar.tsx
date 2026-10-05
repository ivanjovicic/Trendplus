import { useEffect, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import "./AnalyticsControlBar.css";

export type AnalyticsControlBarTone =
  | "neutral"
  | "info"
  | "success"
  | "warning"
  | "critical";

export type AnalyticsControlBarAction = {
  key: string;
  label: string;
  onClick?: () => void;
  to?: string;
  disabled?: boolean;
  tone?: "primary" | "secondary";
};

export type AnalyticsControlBarChip = {
  key: string;
  label: string;
  value: string;
  tone?: AnalyticsControlBarTone;
};

export type AnalyticsControlBarField = {
  key: string;
  label: string;
  control: ReactNode;
  span?: "default" | "wide";
};

type AnalyticsControlBarProps = {
  title: string;
  description?: string;
  chips?: AnalyticsControlBarChip[];
  primaryAction?: AnalyticsControlBarAction;
  secondaryActions?: AnalyticsControlBarAction[];
  fields?: AnalyticsControlBarField[];
  responsiveFilterLayout?: boolean;
  mobileFilterSummary?: string;
};

function isMobileFilterViewport(): boolean {
  return typeof window !== "undefined"
    && typeof window.matchMedia === "function"
    && window.matchMedia("(max-width: 640px)").matches;
}

function toneClassName(tone: AnalyticsControlBarTone = "neutral"): string {
  if (tone === "info") return "analytics-control-bar__chip--info";
  if (tone === "success") return "analytics-control-bar__chip--success";
  if (tone === "warning") return "analytics-control-bar__chip--warning";
  if (tone === "critical") return "analytics-control-bar__chip--critical";
  return "";
}

function renderAction(action: AnalyticsControlBarAction, className: string) {
  if (action.to) {
    return (
      <Link key={action.key} to={action.to} className={className}>
        {action.label}
      </Link>
    );
  }

  return (
    <button
      key={action.key}
      type="button"
      onClick={action.onClick}
      disabled={action.disabled}
      className={className}
    >
      {action.label}
    </button>
  );
}

export default function AnalyticsControlBar({
  title,
  description,
  chips = [],
  primaryAction,
  secondaryActions = [],
  fields = [],
  responsiveFilterLayout = false,
  mobileFilterSummary = "Pregledaj filtere",
}: AnalyticsControlBarProps) {
  const [filterDisclosureOpen, setFilterDisclosureOpen] = useState(
    () => !isMobileFilterViewport(),
  );

  useEffect(() => {
    if (!responsiveFilterLayout || typeof window.matchMedia !== "function") return;

    const mediaQuery = window.matchMedia("(max-width: 640px)");
    const updateDisclosure = () => setFilterDisclosureOpen(!mediaQuery.matches);
    updateDisclosure();
    if (mediaQuery.addEventListener) {
      mediaQuery.addEventListener("change", updateDisclosure);
    } else {
      mediaQuery.addListener?.(updateDisclosure);
    }

    return () => {
      if (mediaQuery.removeEventListener) {
        mediaQuery.removeEventListener("change", updateDisclosure);
      } else {
        mediaQuery.removeListener?.(updateDisclosure);
      }
    };
  }, [responsiveFilterLayout]);

  const fieldsContent = (
    <div className="analytics-control-bar__fields analytics-control-bar__fields--overflow-safe">
      {fields.map((field) => (
        <label
          key={field.key}
          className={`analytics-control-bar__field ${
            field.span === "wide"
              ? "analytics-control-bar__field--wide"
              : ""
          }`.trim()}
        >
          <span className="analytics-control-bar__field-label">
            {field.label}
          </span>
          {field.control}
        </label>
      ))}
    </div>
  );

  return (
    <section
      className={`analytics-control-bar${responsiveFilterLayout ? " analytics-control-bar--responsive-pilot" : ""}`}
      aria-label={title}
      data-testid="analytics-control-bar"
    >
      <div className="analytics-control-bar__header">
        <div className="analytics-control-bar__copy">
          <p className="analytics-control-bar__eyebrow">
            {"Zajedni\u010Dke kontrole"}
          </p>
          <h2 className="analytics-control-bar__title">{title}</h2>
          {description ? (
            <p className="analytics-control-bar__description">{description}</p>
          ) : null}
        </div>
        {primaryAction || secondaryActions.length > 0 ? (
          <div className="analytics-control-bar__actions">
            {secondaryActions.map((action) =>
              renderAction(
                action,
                "analytics-control-bar__action analytics-control-bar__action--secondary",
              ),
            )}
            {primaryAction
              ? renderAction(
                  primaryAction,
                  "analytics-control-bar__action analytics-control-bar__action--primary",
                )
              : null}
          </div>
        ) : null}
      </div>

      {chips.length > 0 ? (
        <div className="analytics-control-bar__chips">
          {chips.map((chip) => (
            <div
              key={chip.key}
              className={`analytics-control-bar__chip ${toneClassName(
                chip.tone,
              )}`.trim()}
            >
              <span className="analytics-control-bar__chip-label">
                {chip.label}
              </span>
              <span className="analytics-control-bar__chip-value">
                {chip.value}
              </span>
            </div>
          ))}
        </div>
      ) : null}

      {fields.length > 0 ? (
        responsiveFilterLayout ? (
          <details
            className="analytics-control-bar__mobile-filters"
            open={filterDisclosureOpen}
            onToggle={(event) => setFilterDisclosureOpen(event.currentTarget.open)}
          >
            <summary className="analytics-control-bar__mobile-filter-summary">
              <span>Filteri</span>
              <span className="analytics-control-bar__mobile-filter-context">
                {mobileFilterSummary}
              </span>
            </summary>
            {fieldsContent}
          </details>
        ) : fieldsContent
      ) : null}
    </section>
  );
}
