import React, { createContext, useContext, useState, useCallback, ReactNode } from "react";

export type ToastType = "success" | "error" | "warning" | "info";

export interface Toast {
    id: string;
    type: ToastType;
    message: string;
    duration?: number;
}

interface ToastContextType {
    showToast: (message: string, type?: ToastType, duration?: number) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export function useToast() {
    const context = useContext(ToastContext);
    if (!context) {
        throw new Error("useToast must be used within ToastProvider");
    }
    return context;
}

export function ToastProvider({ children }: { children: ReactNode }) {
    const [toasts, setToasts] = useState<Toast[]>([]);

    const showToast = useCallback((message: string, type: ToastType = "info", duration = 5000) => {
        const id = Math.random().toString(36).substring(7);
        const newToast: Toast = { id, type, message, duration };

        setToasts((prev) => [...prev, newToast]);

        if (duration > 0) {
            setTimeout(() => {
                setToasts((prev) => prev.filter((t) => t.id !== id));
            }, duration);
        }
    }, []);

    const removeToast = (id: string) => {
        setToasts((prev) => prev.filter((t) => t.id !== id));
    };

    const getToastStyles = (type: ToastType) => {
        const baseStyles = {
            padding: "16px 20px",
            borderRadius: "8px",
            marginBottom: "12px",
            display: "flex",
            alignItems: "center",
            gap: "12px",
            boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
            animation: "slideIn 0.3s ease-out",
            fontWeight: 600,
        };

        const typeStyles = {
            success: {
                background: "#f0fdf4",
                color: "#059669",
                border: "2px solid #059669",
            },
            error: {
                background: "#fef2f2",
                color: "#dc2626",
                border: "2px solid #dc2626",
            },
            warning: {
                background: "#fef3c7",
                color: "#f59e0b",
                border: "2px solid #f59e0b",
            },
            info: {
                background: "#eff6ff",
                color: "#2563eb",
                border: "2px solid #2563eb",
            },
        };

        return { ...baseStyles, ...typeStyles[type] };
    };

    const getIcon = (type: ToastType) => {
        const icons = {
            success: "?",
            error: "?",
            warning: "??",
            info: "??",
        };
        return icons[type];
    };

    return (
        <ToastContext.Provider value={{ showToast }}>
            {children}
            <div style={{
                position: "fixed",
                top: "20px",
                right: "20px",
                zIndex: 9999,
                minWidth: "300px",
                maxWidth: "500px",
            }}>
                {toasts.map((toast) => (
                    <div
                        key={toast.id}
                        style={getToastStyles(toast.type)}
                    >
                        <span style={{ fontSize: "1.5rem" }}>{getIcon(toast.type)}</span>
                        <span style={{ flex: 1 }}>{toast.message}</span>
                        <button
                            onClick={() => removeToast(toast.id)}
                            style={{
                                background: "transparent",
                                border: "none",
                                cursor: "pointer",
                                fontSize: "1.25rem",
                                padding: 0,
                                opacity: 0.6,
                            }}
                        >
                            ?
                        </button>
                    </div>
                ))}
            </div>
            <style>
                {`
                    @keyframes slideIn {
                        from {
                            transform: translateX(100%);
                            opacity: 0;
                        }
                        to {
                            transform: translateX(0);
                            opacity: 1;
                        }
                    }
                `}
            </style>
        </ToastContext.Provider>
    );
}
