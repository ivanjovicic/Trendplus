import { Link } from "react-router-dom";
import type { DataQualityIssueType } from "../../types/analytics";

type UnknownContext = Record<string, string | number | null | undefined>;

const UNKNOWN_LABELS = new Set([
  "",
  "NEPOZNATO",
  "NEPOZNAT DOBAVLJAC",
  "UNKNOWN SUPPLIER",
]);

function isUnknownValue(value: string | null | undefined): boolean {
  const normalized = (value ?? "").trim().toUpperCase();
  return UNKNOWN_LABELS.has(normalized);
}

export default function AnalyticsUnknownLink(props: {
  value: string | null | undefined;
  issueType: DataQualityIssueType;
  label?: string;
  context?: UnknownContext;
  className?: string;
}) {
  if (!isUnknownValue(props.value)) {
    return <>{props.value}</>;
  }

  const params = new URLSearchParams({ type: props.issueType });
  for (const [key, value] of Object.entries(props.context ?? {})) {
    if (value == null || value === "") continue;
    params.set(key, String(value));
  }

  return (
    <Link
      to={`/analytics/data-quality?${params.toString()}`}
      className={props.className ?? "text-accent-warning underline decoration-dotted underline-offset-2"}
      onClick={(event) => {
        event.stopPropagation();
      }}
      onKeyDown={(event) => {
        event.stopPropagation();
      }}
    >
      {props.label ?? "Nepoznato"}
    </Link>
  );
}
