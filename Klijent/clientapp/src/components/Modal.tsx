import React, { useEffect, useRef } from "react";

export interface ModalProps {
    isOpen: boolean;
    onClose: () => void;
    title: string;
    children: React.ReactNode;
    footer?: React.ReactNode;
    size?: "sm" | "md" | "lg";
}

export default function Modal({ isOpen, onClose, title, children, footer, size = "md" }: ModalProps) {
    const modalRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!isOpen) return;

        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === "Escape") onClose();
        };

        document.addEventListener("keydown", handleEscape);
        document.body.style.overflow = "hidden";

        return () => {
            document.removeEventListener("keydown", handleEscape);
            document.body.style.overflow = "unset";
        };
    }, [isOpen, onClose]);

    if (!isOpen) return null;

    const sizeStyles = {
        sm: { minWidth: 320, maxWidth: 400 },
        md: { minWidth: 400, maxWidth: 600 },
        lg: { minWidth: 600, maxWidth: 800 },
    };

    return (
        <div
            style={{
                position: "fixed",
                inset: 0,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                zIndex: 9999,
                padding: "1rem",
            }}
        >
            {/* Backdrop */}
            <div
                style={{
                    background: "rgba(0,0,0,0.5)",
                    position: "absolute",
                    inset: 0,
                }}
                onClick={onClose}
            />

            {/* Modal Content */}
            <div
                ref={modalRef}
                style={{
                    background: "#fff",
                    borderRadius: "12px",
                    boxShadow: "0 20px 25px -5px rgba(0,0,0,0.3)",
                    zIndex: 10000,
                    ...sizeStyles[size],
                    maxHeight: "90vh",
                    display: "flex",
                    flexDirection: "column",
                }}
            >
                {/* Header */}
                <div
                    style={{
                        padding: "1.5rem",
                        borderBottom: "1px solid #e5e7eb",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                    }}
                >
                    <h3
                        style={{
                            fontSize: "1.25rem",
                            fontWeight: 600,
                            margin: 0,
                        }}
                    >
                        {title}
                    </h3>
                    <button
                        onClick={onClose}
                        style={{
                            background: "none",
                            border: "none",
                            fontSize: "1.5rem",
                            cursor: "pointer",
                            color: "#6b7280",
                            padding: "0.25rem",
                            lineHeight: 1,
                        }}
                        aria-label="Zatvori"
                    >
                        ?
                    </button>
                </div>

                {/* Body */}
                <div
                    style={{
                        padding: "1.5rem",
                        overflowY: "auto",
                        flex: 1,
                    }}
                >
                    {children}
                </div>

                {/* Footer */}
                {footer && (
                    <div
                        style={{
                            padding: "1rem 1.5rem",
                            borderTop: "1px solid #e5e7eb",
                            display: "flex",
                            gap: "0.75rem",
                            justifyContent: "flex-end",
                        }}
                    >
                        {footer}
                    </div>
                )}
            </div>
        </div>
    );
}
