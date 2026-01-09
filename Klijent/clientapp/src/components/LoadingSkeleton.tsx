import React from "react";

interface LoadingSkeletonProps {
    type?: "stats" | "messages" | "table";
    count?: number;
}

export function LoadingSkeleton({ type = "stats", count = 1 }: LoadingSkeletonProps) {
    if (type === "stats") {
        return (
            <div style={{ 
                display: "grid", 
                gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", 
                gap: "1rem", 
                marginBottom: "2rem" 
            }}>
                {Array.from({ length: 5 }).map((_, idx) => (
                    <div
                        key={idx}
                        className="skeleton-card"
                        style={{
                            background: "linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%)",
                            backgroundSize: "200% 100%",
                            animation: "shimmer 1.5s infinite",
                            padding: "1.5rem",
                            borderRadius: "12px",
                            height: "100px",
                        }}
                    />
                ))}
            </div>
        );
    }

    if (type === "messages") {
        return (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                {Array.from({ length: count }).map((_, idx) => (
                    <div
                        key={idx}
                        className="skeleton-card"
                        style={{
                            background: "linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%)",
                            backgroundSize: "200% 100%",
                            animation: "shimmer 1.5s infinite",
                            padding: "1rem",
                            borderRadius: "12px",
                            height: "150px",
                        }}
                    />
                ))}
            </div>
        );
    }

    if (type === "table") {
        return (
            <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                        <tr style={{ background: "#f3f4f6" }}>
                            {Array.from({ length: 5 }).map((_, idx) => (
                                <th key={idx} style={{ padding: "12px" }}>
                                    <div
                                        className="skeleton-text"
                                        style={{
                                            background: "#e5e7eb",
                                            height: "16px",
                                            borderRadius: "4px",
                                        }}
                                    />
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {Array.from({ length: count }).map((_, rowIdx) => (
                            <tr key={rowIdx} style={{ borderBottom: "1px solid #e5e7eb" }}>
                                {Array.from({ length: 5 }).map((_, colIdx) => (
                                    <td key={colIdx} style={{ padding: "12px" }}>
                                        <div
                                            className="skeleton-text"
                                            style={{
                                                background: "linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%)",
                                                backgroundSize: "200% 100%",
                                                animation: "shimmer 1.5s infinite",
                                                height: "16px",
                                                borderRadius: "4px",
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
