import React, { Component, ErrorInfo, ReactNode } from "react";

interface Props {
    children: ReactNode;
    fallback?: ReactNode;
}

interface State {
    hasError: boolean;
    error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
    public state: State = {
        hasError: false,
        error: null,
    };

    public static getDerivedStateFromError(error: Error): State {
        return { hasError: true, error };
    }

    public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
        console.error("ErrorBoundary caught an error:", error, errorInfo);
    }

    private handleReset = () => {
        this.setState({ hasError: false, error: null });
    };

    public render() {
        if (this.state.hasError) {
            if (this.props.fallback) {
                return this.props.fallback;
            }

            return (
                <div style={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    minHeight: "400px",
                    padding: "2rem",
                    textAlign: "center",
                }}>
                    <div style={{
                        background: "#fef2f2",
                        border: "2px solid #dc2626",
                        borderRadius: "12px",
                        padding: "2rem",
                        maxWidth: "600px",
                    }}>
                        <div style={{
                            fontSize: "3rem",
                            marginBottom: "1rem",
                        }}>
                            !
                        </div>
                        <h2 style={{
                            fontSize: "1.5rem",
                            fontWeight: 700,
                            color: "#dc2626",
                            marginBottom: "1rem",
                        }}>
                            Nešto je pošlo naopako
                        </h2>
                        <p style={{
                            fontSize: "1rem",
                            color: "#6b7280",
                            marginBottom: "1.5rem",
                        }}>
                            Došlo je do neočekivane greške. Pokušajte ponovo ili kontaktirajte podršku.
                        </p>
                        {this.state.error && (
                            <details style={{
                                background: "#f9fafb",
                                padding: "1rem",
                                borderRadius: "8px",
                                marginBottom: "1.5rem",
                                textAlign: "left",
                            }}>
                                <summary style={{
                                    cursor: "pointer",
                                    fontWeight: 600,
                                    color: "#374151",
                                    marginBottom: "0.5rem",
                                }}>
                                    Tehnički detalji
                                </summary>
                                <pre style={{
                                    fontSize: "0.875rem",
                                    color: "#dc2626",
                                    overflow: "auto",
                                    fontFamily: "monospace",
                                }}>
                                    {this.state.error.message}
                                    {this.state.error.stack && `\n\n${this.state.error.stack}`}
                                </pre>
                            </details>
                        )}
                        <button
                            onClick={this.handleReset}
                            style={{
                                background: "#2563eb",
                                color: "white",
                                border: "none",
                                borderRadius: "8px",
                                padding: "12px 24px",
                                fontSize: "1rem",
                                fontWeight: 600,
                                cursor: "pointer",
                            }}
                        >
                            Pokušaj ponovo
                        </button>
                    </div>
                </div>
            );
        }

        return this.props.children;
    }
}
