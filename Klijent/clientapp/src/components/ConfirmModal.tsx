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
    cancelText = "Otkaži",
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
                        className="button-big button-secondary"
                        style={{ width: "auto", padding: "10px 18px", marginTop: 0 }}
                        onClick={onCancel}
                        disabled={isBusy}
                    >
                        {cancelText}
                    </button>
                    <button
                        type="button"
                        className={confirmClassName}
                        style={{ width: "auto", padding: "10px 18px", marginTop: 0, boxShadow: "none" }}
                        onClick={onConfirm}
                        disabled={isBusy}
                    >
                        {isBusy ? "Radim..." : confirmText}
                    </button>
                </>
            }
        >
            <div style={{ color: "#374151", lineHeight: 1.5 }}>{message}</div>
        </Modal>
    );
}
