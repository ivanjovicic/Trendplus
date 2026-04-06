import React from "react";

interface LoadingSkeletonProps {
    type?: "stats" | "messages" | "table";
    count?: number;
}

export function LoadingSkeleton({ type = "stats", count = 1 }: LoadingSkeletonProps) {
    if (type === "stats") {
        return (
            <div className="grid gap-4 mb-8" style={{ gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))" }}>
                {Array.from({ length: 5 }).map((_, idx) => (
                    <div
                        key={idx}
                        className="skeleton-card rounded-lg p-6"
                        style={{
                            background: "linear-gradient(90deg, var(--surface-elevated, var(--theme-color-f3f4f6, #f3f4f6)) 25%, var(--surface-default, var(--theme-color-e5e7eb, var(--theme-color-e5e7eb, #e5e7eb))) 50%, var(--surface-elevated, var(--theme-color-f3f4f6, var(--theme-color-f3f4f6, #f3f4f6))) 75%)",
                            backgroundSize: "200% 100%",
                            animation: "shimmer 1.5s infinite",
                            height: "100px",
                        }}
                    />
                ))}
            </div>
        );
    }

    if (type === "messages") {
        return (
            <div className="flex flex-col gap-4">
                {Array.from({ length: count }).map((_, idx) => (
                    <div
                        key={idx}
                        className="skeleton-card rounded-lg p-4"
                        style={{
                            background: "linear-gradient(90deg, var(--surface-elevated, var(--theme-color-f3f4f6, var(--theme-color-f3f4f6, #f3f4f6))) 25%, var(--surface-default, var(--theme-color-e5e7eb, var(--theme-color-e5e7eb, #e5e7eb))) 50%, var(--surface-elevated, var(--theme-color-f3f4f6, var(--theme-color-f3f4f6, #f3f4f6))) 75%)",
                            backgroundSize: "200% 100%",
                            animation: "shimmer 1.5s infinite",
                            height: "150px",
                        }}
                    />
                ))}
            </div>
        );
    }

    if (type === "table") {
        return (
            <div className="overflow-x-auto">
                    <table className="w-full border-collapse">
                        <thead>
                            <tr className="bg-surface-elevated">
                                {Array.from({ length: 5 }).map((_, idx) => (
                                    <th key={idx} className="p-3">
                                        <div
                                            className="skeleton-text rounded"
                                            style={{
                                                background: "var(--surface-default, var(--theme-color-e5e7eb, var(--theme-color-e5e7eb, #e5e7eb)))",
                                                height: "16px",
                                            }}
                                        />
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {Array.from({ length: count }).map((_, rowIdx) => (
                                <tr key={rowIdx} className="border-b" style={{ borderColor: 'var(--border-default)' }}>
                                    {Array.from({ length: 5 }).map((_, colIdx) => (
                                        <td key={colIdx} className="p-3">
                                            <div
                                                className="skeleton-text rounded"
                                                style={{
                                                    background: "linear-gradient(90deg, var(--surface-elevated, var(--theme-color-f3f4f6, var(--theme-color-f3f4f6, #f3f4f6))) 25%, var(--surface-default, var(--theme-color-e5e7eb, var(--theme-color-e5e7eb, #e5e7eb))) 50%, var(--surface-elevated, var(--theme-color-f3f4f6, var(--theme-color-f3f4f6, #f3f4f6))) 75%)",
                                                    backgroundSize: "200% 100%",
                                                    animation: "shimmer 1.5s infinite",
                                                    height: "16px",
                                                    width: colIdx % 2 === 0 ? "80%" : "60%",
                                                }}
                                            />
                                        </td>
                                    ))}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
        );
    }

    return null;
}

// Add CSS animation (inject into global styles or use styled-components)
export const skeletonStyles = `
@keyframes shimmer {
    0% {
        background-position: 200% 0;
    }
    100% {
        background-position: -200% 0;
    }
}

.skeleton-card, .skeleton-text {
    animation: shimmer 1.5s infinite;
}
`;
