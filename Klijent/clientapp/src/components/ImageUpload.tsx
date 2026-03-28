import React from "react";

interface ImageUploadProps {
    onUpload: (formData: FormData) => void;
    label?: string;
    accept?: string;
    capture?: "user" | "environment";
    buttonText?: string;
    showPreview?: boolean;
}

export default function ImageUpload({
    onUpload,
    label = "Dodaj sliku",
    accept = "image/*",
    capture = "environment",
    buttonText = "📷 Izaberi sliku / Slikaj",
    showPreview = true,
}: ImageUploadProps) {
    const [preview, setPreview] = React.useState<string | null>(null);
    const [fileName, setFileName] = React.useState<string | null>(null);
    const [isDragging, setIsDragging] = React.useState(false);
    const fileInputRef = React.useRef<HTMLInputElement>(null);

    const processFile = (file: File) => {
        // Validate file type
        if (!file.type.startsWith("image/")) {
            alert("Molimo izaberite sliku");
            return;
        }

        // Validate file size (max 10MB)
        if (file.size > 10 * 1024 * 1024) {
            alert("Slika je prevelika. Maksimalna veličina je 10MB");
            return;
        }

        // Create preview
        if (showPreview) {
            const reader = new FileReader();
            reader.onloadend = () => {
                setPreview(reader.result as string);
            };
            reader.readAsDataURL(file);
        }

        setFileName(file.name);

        // Create FormData and call onUpload
        const formData = new FormData();
        formData.append("image", file);
        onUpload(formData);
    };

    const handleSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;
        processFile(file);
    };

    const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragging(true);
    };

    const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragging(false);
    };

    const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragging(false);

        const file = e.dataTransfer.files?.[0];
        if (file) {
            processFile(file);
        }
    };

    const handleClear = () => {
        setPreview(null);
        setFileName(null);
        if (fileInputRef.current) {
            fileInputRef.current.value = "";
        }
    };

    const handleButtonClick = () => {
        fileInputRef.current?.click();
    };

    return (
        <div style={{ marginBottom: "1rem" }}>
            {label && (
                <label
                    className="field-label"
                    style={{ fontWeight: 600, color: "var(--text-secondary, #374151)" }}
                >
                    {label}
                </label>
            )}

            {/* Hidden file input */}
            <input
                ref={fileInputRef}
                type="file"
                accept={accept}
                capture={capture}
                onChange={handleSelect}
                style={{ display: "none" }}
            />

            {/* Drag & Drop Area */}
            <div
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                onClick={handleButtonClick}
                style={{
                    marginTop: "8px",
                    padding: "2rem",
                    border: isDragging
                        ? "3px dashed var(--focus-ring, #3b82f6)"
                        : "2px dashed var(--border-default, #d1d5db)",
                    borderRadius: "12px",
                    background: isDragging
                        ? "linear-gradient(135deg, var(--surface-elevated, #eff6ff) 0%, var(--surface-light, #dbeafe) 100%)"
                        : "linear-gradient(135deg, var(--surface-default, #f9fafb) 0%, var(--surface-light, #ffffff) 100%)",
                    textAlign: "center",
                    cursor: "pointer",
                    transition: "all 0.2s ease",
                    position: "relative",
                    overflow: "hidden",
                }}
                    onMouseEnter={(e) => {
                        if (!isDragging) {
                            e.currentTarget.style.borderColor = "var(--border-hover, #9ca3af)";
                            e.currentTarget.style.background =
                                "linear-gradient(135deg, var(--surface-elevated, #f3f4f6) 0%, var(--surface-default, #e5e7eb) 100%)";
                        }
                    }}
                    onMouseLeave={(e) => {
                        if (!isDragging) {
                            e.currentTarget.style.borderColor = "var(--border-default, #d1d5db)";
                            e.currentTarget.style.background =
                                "linear-gradient(135deg, var(--surface-default, #f9fafb) 0%, var(--surface-light, #ffffff) 100%)";
                        }
                    }}
            >
                {isDragging ? (
                    <>
                        <div style={{ fontSize: "3rem", marginBottom: "0.5rem" }}>📥</div>
                        <p
                            style={{
                            fontSize: "1.125rem",
                            fontWeight: 600,
                            color: "var(--focus-ring, #2563eb)",
                            margin: 0,
                            }}
                        >
                            Pusti sliku ovde
                        </p>
                    </>
                ) : (
                    <>
                        <div style={{ fontSize: "3rem", marginBottom: "0.5rem" }}>📷</div>
                        <p
                            style={{
                            fontSize: "1rem",
                            fontWeight: 600,
                            color: "var(--text-secondary, #374151)",
                            marginBottom: "0.5rem",
                            }}
                        >
                            {buttonText}
                        </p>
                        <p
                            style={{
                            fontSize: "0.875rem",
                            color: "var(--text-muted, #6b7280)",
                            margin: 0,
                            }}
                        >
                            ili prevuci sliku ovde
                        </p>
                        <p
                            style={{
                            fontSize: "0.75rem",
                            color: "var(--text-muted, #9ca3af)",
                            marginTop: "0.5rem",
                            }}
                        >
                            Podržani formati: JPG, PNG, GIF, WEBP (max 10MB)
                        </p>
                    </>
                )}
            </div>

            {/* Preview section */}
            {showPreview && preview && (
                <div
                    style={{
                        marginTop: "1rem",
                        padding: "1rem",
                        background: "var(--surface-light, #f3f4f6)",
                        border: "2px solid var(--border-default, #e5e7eb)",
                        borderRadius: "12px",
                        animation: "fadeIn 0.3s ease-in",
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            marginBottom: "0.75rem",
                        }}
                    >
                        <span
                            style={{
                                fontSize: "0.875rem",
                                color: "var(--text-muted, #6b7280)",
                                fontWeight: 600,
                            }}
                        >
                            ✓ Izabrana slika: {fileName}
                        </span>
                        <button
                            type="button"
                            onClick={(e) => {
                                e.stopPropagation();
                                handleClear();
                            }}
                            style={{
                                background: "var(--error, #dc2626)",
                                color: "var(--text-on-primary, #ffffff)",
                                padding: "6px 12px",
                                borderRadius: "6px",
                                border: "none",
                                cursor: "pointer",
                                fontSize: "0.75rem",
                                fontWeight: 600,
                                transition: "transform 0.2s ease",
                            }}
                            onMouseEnter={(e) => (e.currentTarget.style.transform = "scale(1.05)")}
                            onMouseLeave={(e) => (e.currentTarget.style.transform = "scale(1)")}
                        >
                            ✕ Ukloni
                        </button>
                    </div>
                    <img
                        src={preview}
                        alt="Preview"
                        style={{
                            maxWidth: "100%",
                            maxHeight: "400px",
                            borderRadius: "8px",
                            border: "1px solid var(--border-default, #d1d5db)",
                            objectFit: "contain",
                            display: "block",
                            margin: "0 auto",
                        }}
                    />
                </div>
            )}

            {/* File name display without preview */}
            {!showPreview && fileName && (
                <div
                    style={{
                        marginTop: "1rem",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: "10px",
                        background: "var(--surface-muted, #f0fdf4)",
                        border: "1px solid var(--c-86efac, #86efac)",
                        padding: "10px 12px",
                        borderRadius: "8px",
                    }}
                >
                    <span
                        style={{
                            fontSize: "0.875rem",
                            color: "var(--success, #15803d)",
                            fontWeight: 600,
                        }}
                    >
                        ✓ {fileName}
                    </span>
                    <button
                        type="button"
                        onClick={(e) => {
                            e.stopPropagation();
                            handleClear();
                        }}
                        style={{
                            background: "var(--error, #dc2626)",
                            color: "var(--text-on-primary, #ffffff)",
                            padding: "4px 8px",
                            borderRadius: "4px",
                            border: "none",
                            cursor: "pointer",
                            fontSize: "0.75rem",
                        }}
                    >
                        ✕
                    </button>
                </div>
            )}

            <style>
                {`
                    @keyframes fadeIn {
                        from {
                            opacity: 0;
                            transform: translateY(-10px);
                        }
                        to {
                            opacity: 1;
                            transform: translateY(0);
                        }
                    }
                `}
            </style>
        </div>
    );
}
