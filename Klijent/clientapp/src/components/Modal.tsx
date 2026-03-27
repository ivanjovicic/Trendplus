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
    const closeButtonRef = useRef<HTMLButtonElement>(null);
    const previouslyFocusedRef = useRef<HTMLElement | null>(null);

    useEffect(() => {
        if (!isOpen) return;

        previouslyFocusedRef.current = document.activeElement as HTMLElement | null;

        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === "Escape") onClose();
        };

        const handleTabTrap = (e: KeyboardEvent) => {
            if (e.key !== "Tab" || !modalRef.current) return;

            const focusable = modalRef.current.querySelectorAll<HTMLElement>(
                'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
            );

            if (focusable.length === 0) {
                e.preventDefault();
                modalRef.current.focus();
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            const active = document.activeElement as HTMLElement | null;

            if (e.shiftKey && active === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && active === last) {
                e.preventDefault();
                first.focus();
            }
        };

        document.addEventListener("keydown", handleEscape);
        document.addEventListener("keydown", handleTabTrap);
        document.body.style.overflow = "hidden";
        window.setTimeout(() => closeButtonRef.current?.focus(), 0);

        return () => {
            document.removeEventListener("keydown", handleEscape);
            document.removeEventListener("keydown", handleTabTrap);
            document.body.style.overflow = "unset";
            previouslyFocusedRef.current?.focus();
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
                role="dialog"
                aria-modal="true"
                aria-labelledby="modal-title"
                tabIndex={-1}
                style={{
                    background: "var(--surface-default, #ffffff)",
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
                        borderBottom: "1px solid var(--border-default, #e5e7eb)",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                    }}
                >
                    <h3
                        id="modal-title"
                        style={{
                            fontSize: "1.25rem",
                            fontWeight: 600,
                            margin: 0,
                        }}
                    >
                        {title}
                    </h3>
                    <button
                        ref={closeButtonRef}
                        onClick={onClose}
                        style={{
                            background: "none",
                            border: "none",
                            fontSize: "1.5rem",
                            cursor: "pointer",
                            color: "var(--text-muted, #6b7280)",
                            padding: "0.25rem",
                            lineHeight: 1,
                        }}
                        aria-label="Zatvori"
                    >
                        x
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
                            borderTop: "1px solid var(--border-default, #e5e7eb)",
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
