import React, { useEffect, useMemo, useState } from "react";
import Modal from "./Modal";

export interface PromptNumberModalProps {
    isOpen: boolean;
    title: string;
    label: string;
    description?: React.ReactNode;
    defaultValue: number;
    min?: number;
    max?: number;
    confirmText?: string;
    cancelText?: string;
    isBusy?: boolean;
    onConfirm: (value: number) => void;
    onCancel: () => void;
}

export default function PromptNumberModal({
    isOpen,
    title,
    label,
    description,
    defaultValue,
    min = 1,
    max = 365,
    confirmText = "Potvrdi",
    cancelText = "Otkaži",
    isBusy = false,
    onConfirm,
    onCancel,
}: PromptNumberModalProps) {
    const [rawValue, setRawValue] = useState<string>(String(defaultValue));

    useEffect(() => {
        if (isOpen) setRawValue(String(defaultValue));
    }, [isOpen, defaultValue]);

    const parsed = useMemo(() => {
        const n = Number(rawValue);
        return Number.isFinite(n) ? n : NaN;
    }, [rawValue]);

    const validationError = useMemo(() => {
        if (!rawValue.trim()) return "Vrednost je obavezna.";
        if (!Number.isFinite(parsed)) return "Unesite ispravan broj.";
        if (!Number.isInteger(parsed)) return "Unesite ceo broj.";
        if (parsed < min) return `Minimalna vrednost je ${min}.`;
        if (parsed > max) return `Maksimalna vrednost je ${max}.`;
        return null;
    }, [rawValue, parsed, min, max]);

    const canConfirm = !validationError && !isBusy;

    return (
        <Modal
            isOpen={isOpen}
            onClose={onCancel}
            title={title}
            size="sm"
            footer={
                <>
                    <button
                        type="button"
                        className="button-big button-secondary"
                        style={{ width: "auto", padding: "10px 18px", marginTop: 0 }}
                        onClick={onCancel}
                        disabled={isBusy}
                    >
                        {cancelText}
                    </button>
                    <button
                        type="button"
                        className="button-big"
                        style={{ width: "auto", padding: "10px 18px", marginTop: 0, boxShadow: "none" }}
                        onClick={() => onConfirm(parsed)}
                        disabled={!canConfirm}
                    >
                        {isBusy ? "Radim..." : confirmText}
                    </button>
                </>
            }
        >
            {description && (
                <div style={{ marginBottom: 12, color: "#4b5563", lineHeight: 1.5 }}>{description}</div>
            )}

            <label className="field-label" style={{ marginTop: 0 }}>
                {label}
            </label>
            <input
                className="input-big"
                type="number"
                inputMode="numeric"
                value={rawValue}
                onChange={(e) => setRawValue(e.target.value)}
                min={min}
                max={max}
                step={1}
                autoFocus
            />

            {validationError && (
                <div style={{ marginTop: 10 }}>
                    <span className="error-msg" style={{ display: "block" }}>{validationError}</span>
                </div>
            )}
        </Modal>
    );
}
