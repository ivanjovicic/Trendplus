type MarketplaceLoadErrorNoticeProps = {
    message: string;
    onRetry: () => void;
};

export function MarketplaceLoadErrorNotice({ message, onRetry }: MarketplaceLoadErrorNoticeProps) {
    return (
        <div
            role="alert"
            style={{
                display: "flex", flexWrap: "wrap", alignItems: "center", justifyContent: "space-between", gap: 10,
                margin: "0 0 14px", padding: "10px 14px", borderRadius: 10,
                background: "var(--c-fff1f2, var(--theme-color-fff1f2, #fff1f2))",
                color: "var(--c-be123c, var(--theme-color-be123c, #be123c))",
                border: "1px solid var(--c-fecdd3, var(--theme-color-fecdd3, #fecdd3))",
                fontSize: 13, fontWeight: 600,
            }}
        >
            <span>{message}</span>
            <button
                type="button"
                onClick={onRetry}
                style={{
                    padding: "5px 14px", borderRadius: 8, cursor: "pointer", fontSize: 12, fontWeight: 700,
                    background: "var(--surface-card, var(--theme-color-ffffff, #ffffff))",
                    color: "var(--c-be123c, var(--theme-color-be123c, #be123c))",
                    border: "1px solid var(--c-fecdd3, var(--theme-color-fecdd3, #fecdd3))",
                }}
            >
                Pokušaj ponovo
            </button>
        </div>
    );
}
