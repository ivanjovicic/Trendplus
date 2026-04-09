type UltraSpinnerSize = "sm" | "md" | "lg";

const SIZE_CLASS: Record<UltraSpinnerSize, string> = {
  sm: "ultra-spinner--sm",
  md: "ultra-spinner--md",
  lg: "ultra-spinner--lg",
};

export default function UltraSpinner({
  size = "md",
  label = "Loading",
  className = "",
}: {
  size?: UltraSpinnerSize;
  label?: string;
  className?: string;
}) {
  const sizeClass = SIZE_CLASS[size];

  return (
    <span className={`ultra-spinner ${sizeClass} ${className}`.trim()} role="status" aria-label={label}>
      <span className="ultra-spinner__ring" />
      <span className="ultra-spinner__ring ultra-spinner__ring--alt" />
      <span className="ultra-spinner__core" />
      <span className="sr-only">{label}</span>
    </span>
  );
}
