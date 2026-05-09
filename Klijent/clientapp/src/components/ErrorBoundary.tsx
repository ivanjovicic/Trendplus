import React, { Component, ErrorInfo, ReactNode } from "react";
import { recoverFromChunkLoadError } from "../utils/chunkLoadRecovery";

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
        recoverFromChunkLoadError(error);
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
                <div className="flex flex-col items-center justify-center" style={{ minHeight: 400, padding: '2rem', textAlign: 'center' }}>
                    <div className="rounded-lg p-8" style={{ background: 'var(--surface-elevated)', border: '2px solid var(--error)', maxWidth: 600 }}>
                        <div className="text-6xl mb-4">!</div>
                        <h2 className="text-xl font-bold mb-3" style={{ color: 'var(--error)' }}>Nešto je pošlo naopako</h2>
                        <p className="mb-4 text-sm text-muted">Došlo je do neočekivane greške. Pokušajte ponovo ili kontaktirajte podršku.</p>
                        {this.state.error && (
                            <details className="bg-surface-light p-4 rounded mb-4 text-left">
                                <summary className="cursor-pointer font-semibold text-muted mb-2">Tehnički detalji</summary>
                                <pre className="text-sm" style={{ color: 'var(--error)', overflow: 'auto', fontFamily: 'monospace' }}>
                                    {this.state.error.message}
                                    {this.state.error.stack && `\n\n${this.state.error.stack}`}
                                </pre>
                            </details>
                        )}
                        <button onClick={this.handleReset} className="rounded-md px-4 py-3 font-semibold" style={{ background: 'var(--info)', color: 'var(--text-on-primary)', border: 'none' }}>
                            Pokušaj ponovo
                        </button>
                    </div>
                </div>
            );
        }

        return this.props.children;
    }
}
