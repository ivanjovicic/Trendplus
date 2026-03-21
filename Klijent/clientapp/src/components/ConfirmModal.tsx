import React from "react";
import Modal from "./Modal";

export interface ConfirmModalProps {
    isOpen: boolean;
    title: string;
    message: React.ReactNode;
    confirmText?: string;
    cancelText?: string;
    confirmVariant?: "primary" | "danger";
    isBusy?: boolean;
    onConfirm: () => void;
    onCancel: () => void;
}

export default function ConfirmModal({
    isOpen,
    title,
    message,
    confirmText = "Potvrdi",
    cancelText = "Otka�i",
    confirmVariant = "primary",
    isBusy = false,
    onConfirm,
    onCancel,
}: ConfirmModalProps) {
    const confirmClassName =
        confirmVariant === "danger" ? "button-big button-danger" : "button-big";

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
                        className="button-big bg-surface-elevated text-foreground border border-border"
                        onClick={onCancel}
                        disabled={isBusy}
                    >
                        {cancelText}
                    </button>
                    <button
                        type="button"
                        className={`${confirmClassName} ${confirmVariant === 'danger' ? 'button-danger' : 'bg-primary text-white'}`}
                        onClick={onConfirm}
                        disabled={isBusy}
                    >
                        {isBusy ? "Radim..." : confirmText}
                    </button>
                </>
            }
        >
            <div className="text-foreground leading-relaxed">{message}</div>
        </Modal>
    );
}
