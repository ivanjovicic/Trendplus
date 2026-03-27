import React, { useState, useRef, useEffect } from "react";
import { popularBrands } from "./brands";

interface OptionItem { label: string; value: string }

interface Props {
    label?: string;
    value: string | string[];
    onChange: (v: string | string[]) => void;
    options?: OptionItem[];
    placeholder?: string;
    multiple?: boolean;
}

const FOCUS_RING = "var(--focus-ring, #2563eb)";
const BORDER_DEFAULT = "var(--border-default, #d1d5db)";
const SURFACE_DEFAULT = "var(--surface-default, #ffffff)";
const SURFACE_ELEVATED = "var(--surface-elevated, #f3f4f6)";
const SURFACE_LIGHT = "var(--surface-light, #ffffff)";
const TEXT_MUTED = "var(--text-muted, #6b7280)";
const FOCUS_RING_SHADOW = "var(--focus-ring-shadow, rgba(37, 99, 235, 0.08))";

export default function SearchableSelect({ label, value, onChange, options = popularBrands as any, placeholder, multiple = false }: Props) {
    const [query, setQuery] = useState("");
    const [isOpen, setIsOpen] = useState(false);
    const [activeIndex, setActiveIndex] = useState<number>(-1);
    const containerRef = useRef<HTMLDivElement | null>(null);

    const isMultiple = multiple || Array.isArray(value);
    const selectedArray: string[] = isMultiple ? (Array.isArray(value) ? value : []) : [];
    const selectedSingle: string = !isMultiple && typeof value === 'string' ? value : "";

    // Normalize options to OptionItem[]
    const normalizedOptions: OptionItem[] = (options as OptionItem[]).map(opt => {
        if (typeof opt === 'string') return { label: opt, value: opt };
        return opt as OptionItem;
    });

    const filtered = normalizedOptions.filter(o =>
        o.label.toLowerCase().includes(query.toLowerCase()) || o.value.toLowerCase().includes(query.toLowerCase())
    );

    useEffect(() => {
        const onBodyClick = (e: MouseEvent) => {
            if (!containerRef.current) return;
            if (!containerRef.current.contains(e.target as Node)) {
                setIsOpen(false);
                setActiveIndex(-1);
            }
        };
        document.addEventListener("click", onBodyClick);
        return () => document.removeEventListener("click", onBodyClick);
    }, []);

    useEffect(() => {
        // reset active index when filtered changes
        setActiveIndex(filtered.length > 0 ? 0 : -1);
    }, [query]);

    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === "ArrowDown") {
            e.preventDefault();
            setIsOpen(true);
            setActiveIndex((prev) => {
                const next = prev + 1;
                return next >= filtered.length ? 0 : next;
            });
            return;
        }
        if (e.key === "ArrowUp") {
            e.preventDefault();
            setIsOpen(true);
            setActiveIndex((prev) => {
                const next = prev - 1;
                return next < 0 ? Math.max(filtered.length - 1, 0) : next;
            });
            return;
        }
        if (e.key === "Enter") {
            if (isOpen && activeIndex >= 0 && activeIndex < filtered.length) {
                e.preventDefault();
                const sel = filtered[activeIndex];
                if (isMultiple) {
                    toggleSelection(sel.value);
                    setQuery("");
                    setIsOpen(true); // keep open for multi-select convenience
                } else {
                    onChange(sel.value);
                    setQuery("");
                    setIsOpen(false);
                }
                setActiveIndex(-1);
            } else if (isMultiple && query.trim()) {
                // Enter adds free-text as tag (value == label)
                e.preventDefault();
                addFreeText(query.trim());
                setQuery("");
                setIsOpen(false);
            }
            return;
        }
        if (e.key === "Escape") {
            setIsOpen(false);
            setActiveIndex(-1);
            return;
        }
    };

    const addFreeText = (text: string) => {
        if (!text) return;
        if (!isMultiple) {
            onChange(text);
            return;
        }
        if (selectedArray.includes(text)) return;
        const next = [...selectedArray, text];
        onChange(next);
    };

    const toggleSelection = (itemValue: string) => {
        if (!isMultiple) {
            onChange(itemValue);
            return;
        }
        const exists = selectedArray.includes(itemValue);
        const next = exists ? selectedArray.filter(s => s !== itemValue) : [...selectedArray, itemValue];
        onChange(next);
    };

    const inputHasSelection = (!isMultiple && Boolean(selectedSingle) && !query) || (isMultiple && selectedArray.length > 0 && !query);

    // Map selected values to labels for display
    const getLabelForValue = (val: string) => {
        const found = normalizedOptions.find(o => o.value === val);
        return found ? found.label : val;
    };

    // display value in input: for multi show joined selected labels (readonly when query empty)
    const displayValue = () => {
        if (query) return query;
        if (isMultiple) return selectedArray.map(getLabelForValue).join(', ');
        return getLabelForValue(selectedSingle);
    };

    return (
        <div ref={containerRef} style={{ position: "relative", width: "100%" }}>
            {label ? (
                <label style={{ fontWeight: 600, display: "block", marginBottom: 6 }}>
                    {label}
                </label>
            ) : null}

            {/* INPUT (search + manual entry) */}
            <input
                value={displayValue()}
                onChange={(e) => {
                    const v = e.target.value;
                    if (!isMultiple) onChange(v);
                    setQuery(v);
                    setIsOpen(true);
                }}
                onFocus={() => setIsOpen(true)}
                onKeyDown={handleKeyDown}
                placeholder={placeholder || "Type or select..."}
                style={{
                    width: "100%",
                    padding: "0.75rem",
                    borderRadius: 8,
                    border: `1px solid ${inputHasSelection ? FOCUS_RING : BORDER_DEFAULT}`,
                    boxShadow: inputHasSelection ? `0 0 0 4px ${FOCUS_RING_SHADOW}` : undefined,
                    fontSize: 14
                }}
                aria-haspopup="listbox"
                aria-expanded={isOpen}
            />

            {/* DROPDOWN */}
            {(isOpen || query.length > 0) && (
                <div
                role="listbox"
                style={{
                    position: "absolute",
                    top: "100%",
                    left: 0,
                    right: 0,
                    background: SURFACE_DEFAULT,
                    border: `1px solid ${BORDER_DEFAULT}`,
                    borderRadius: 8,
                    marginTop: 4,
                    maxHeight: 240,
                    overflowY: "auto",
                    zIndex: 20,
                    boxShadow: `0 8px 16px ${FOCUS_RING_SHADOW}`,
                }}
            >
                {filtered.length === 0 && (
                    <div style={{ padding: 10, color: TEXT_MUTED }}>
                        No results — press Enter to use "{query}"
                    </div>
                )}

                    {filtered.map((opt, idx) => {
                        const isActive = activeIndex === idx;
                        const isSelected = isMultiple ? selectedArray.includes(opt.value) : selectedSingle === opt.value;
                        return (
                            <div
                                key={opt.value}
                                role="option"
                                aria-selected={isActive}
                                onClick={() => {
                                    toggleSelection(opt.value);
                                    setQuery("");
                                    if (!isMultiple) setIsOpen(false);
                                    setActiveIndex(-1);
                                }}
                                onMouseEnter={() => setActiveIndex(idx)}
                                style={{
                                    display: 'flex',
                                    justifyContent: 'space-between',
                                    alignItems: 'center',
                                    padding: "10px 14px",
                                    cursor: "pointer",
                                    background: isActive
                                        ? FOCUS_RING
                                        : isSelected
                                            ? SURFACE_ELEVATED
                                            : "transparent",
                                    color: isActive
                                        ? "var(--text-on-primary, #ffffff)"
                                        : isSelected
                                            ? "var(--text-primary, #0f172a)"
                                            : "var(--text-primary, #0f172a)",
                                    fontWeight: isActive ? 700 : (isSelected ? 600 : 500),
                                    borderBottom: `1px solid ${SURFACE_LIGHT}`,
                                }}
                            >
                                <span>{opt.label}</span>
                                {/* selection indicator */}
                                {isSelected && (
                                    <span
                                        style={{
                                            fontSize: 12,
                                            color: isActive ? "var(--text-on-primary, #ffffff)" : FOCUS_RING,
                                            fontWeight: 700,
                                        }}
                                    >
                                        ✓
                                    </span>
                                )}
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}
