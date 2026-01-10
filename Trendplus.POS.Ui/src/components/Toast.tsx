import React, { createContext, useCallback, useContext, useState, type ReactNode } from "react";

export type ToastType = "success" | "error" | "warning" | "info";

export interface Toast {
    id: string;
    type: ToastType;
    message: string;
    duration?: number;
}

interface ToastContextType {
    showToast: (message: string, type?: ToastType, duration?: number) => void;
    success: (message: string, duration?: number) => void;
    error: (message: string, duration?: number) => void;
    warning: (message: string, duration?: number) => void;
    info: (message: string, duration?: number) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export function useToast() {
    const ctx = useContext(ToastContext);
    if (!ctx) throw new Error("useToast must be used within ToastProvider");
    return ctx;
}

export function ToastProvider({ children }: { children: ReactNode }) {
    const [toasts, setToasts] = useState<Toast[]>([]);

    const showToast = useCallback((message: string, type: ToastType = "info", duration = 4500) => {
        const id = Math.random().toString(36).substring(2);
        const toast: Toast = { id, type, message, duration };

        setToasts((prev) => [...prev, toast]);

        if (duration > 0) {
            window.setTimeout(() => {
                setToasts((prev) => prev.filter((t) => t.id !== id));
            }, duration);
        }
    }, []);

    const success = useCallback((m: string, d?: number) => showToast(m, "success", d), [showToast]);
    const error = useCallback((m: string, d?: number) => showToast(m, "error", d), [showToast]);
    const warning = useCallback((m: string, d?: number) => showToast(m, "warning", d), [showToast]);
    const info = useCallback((m: string, d?: number) => showToast(m, "info", d), [showToast]);

    const removeToast = (id: string) => setToasts((prev) => prev.filter((t) => t.id !== id));

    const getToastStyles = (type: ToastType): React.CSSProperties => {
        const base: React.CSSProperties = {
            padding: "14px 16px",
            borderRadius: 12,
            marginBottom: 10,
            display: "flex",
            alignItems: "center",
            gap: 10,
            boxShadow: "0 10px 20px rgba(0,0,0,0.20)",
            backdropFilter: "blur(6px)",
            fontWeight: 700,
        };

        const theme: Record<ToastType, React.CSSProperties> = {
            success: { background: "#f0fdf4", color: "#059669", border: "2px solid #059669" },
            error: { background: "#fef2f2", color: "#dc2626", border: "2px solid #dc2626" },
            warning: { background: "#fef3c7", color: "#f59e0b", border: "2px solid #f59e0b" },
            info: { background: "#eff6ff", color: "#2563eb", border: "2px solid #2563eb" },
        };

        return { ...base, ...theme[type] };
    };

    const icon = (type: ToastType) => {
        switch (type) {
            case "success":
                return "?";
            case "error":
                return "?";
            case "warning":
                return "!";
            default:
                return "i";
        }
    };

    return (
        <ToastContext.Provider value={{ showToast, success, error, warning, info }}>
            {children}
            <div
                style={{
                    position: "fixed",
                    top: 16,
                    right: 16,
                    zIndex: 9999,
                    minWidth: 280,
                    maxWidth: 520,
                }}
            >
                {toasts.map((t) => (
                    <div key={t.id} style={getToastStyles(t.type)}>
                        <span style={{ fontSize: 18, width: 20, textAlign: "center" }}>{icon(t.type)}</span>
                        <span style={{ flex: 1, fontSize: 14 }}>{t.message}</span>
                        <button
                            onClick={() => removeToast(t.id)}
                            style={{
                                background: "transparent",
                                border: "none",
                                cursor: "pointer",
                                fontSize: 18,
                                padding: 0,
                                opacity: 0.75,
                                color: "inherit",
                            }}
                            aria-label="Zatvori"
                        >
                            ×
                        </button>
                    </div>
                ))}
            </div>
        </ToastContext.Provider>
    );
}
