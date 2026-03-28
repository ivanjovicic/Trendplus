import React, { useState } from "react";
import ImageUpload from "../components/ImageUpload";
import { uploadImage, deleteProductImage, getImageUrl } from "../services/uploadApi";
import { useToast } from "../components/Toast";

export default function ImageUploadTestPage() {
    const toast = useToast();
    const [uploading, setUploading] = useState(false);
    const [uploadedImage, setUploadedImage] = useState<{
        fileName: string;
        imageUrl: string;
        productId?: number;
    } | null>(null);
    const [productId, setProductId] = useState<string>("");

    const handleImageUpload = async (formData: FormData) => {
        setUploading(true);
        
        try {
            const pid = productId ? parseInt(productId) : undefined;
            const result = await uploadImage(formData, pid);
            
            setUploadedImage({
                fileName: result.fileName,
                imageUrl: result.imageUrl,
                productId: result.productId
            });
            
            toast.success("Slika uspešno otpremljena! ✅");
            console.log("Upload response:", result);
        } catch (error) {
            console.error("Upload error:", error);
            toast.error("Greška pri otpremanju slike");
        } finally {
            setUploading(false);
        }
    };

    const handleDeleteImage = async () => {
        if (!uploadedImage?.productId) {
            toast.error("Nema ID-a proizvoda za brisanje slike");
            return;
        }

        try {
            await deleteProductImage(uploadedImage.productId);
            toast.success("Slika uspešno obrisana! 🗑️");
            setUploadedImage(null);
        } catch (error) {
            console.error("Delete error:", error);
            toast.error("Greška pri brisanju slike");
        }
    };

    return (
        <div className="card" style={{ maxWidth: "800px", margin: "2rem auto" }}>
            <h2 className="text-2xl font-semibold mb-6" style={{ color: "var(--c-1f2937, #1f2937)" }}>
                📸 Upload slike - Test stranica
            </h2>

            {/* Product ID Input */}
            <div
                style={{
                    background: "var(--surface-default, #ffffff)",
                    padding: "1.5rem",
                    borderRadius: "12px",
                    border: "1px solid var(--c-e5e7eb, #e5e7eb)",
                    boxShadow: "var(--box-shadow-md, 0 2px 8px rgba(0,0,0,0.06))",
                    marginBottom: "1.5rem",
                }}
            >
                <label className="field-label" style={{ fontWeight: 600 }}>
                    ID proizvoda (opciono)
                </label>
                <input
                    type="number"
                    className="input-big"
                    placeholder="Unesite ID proizvoda za povezivanje slike..."
                    value={productId}
                    onChange={(e) => setProductId(e.target.value)}
                    style={{ maxWidth: "300px" }}
                />
                <p style={{ fontSize: "0.875rem", color: "var(--c-6b7280, #6b7280)", marginTop: "0.5rem" }}>
                    Ako ostavite prazno, slika će biti otpremljena bez povezivanja sa artiklom
                </p>
            </div>

            {/* Image Upload Component */}
            <div
                style={{
                    background: "var(--surface-default, #ffffff)",
                    padding: "1.5rem",
                    borderRadius: "12px",
                    border: "1px solid var(--c-e5e7eb, #e5e7eb)",
                    boxShadow: "var(--box-shadow-md, 0 2px 8px rgba(0,0,0,0.06))",
                }}
            >
                <ImageUpload
                    onUpload={handleImageUpload}
                    label="Slika artikla"
                    buttonText="📷 Dodaj sliku artikla"
                    showPreview={true}
                />

                {uploading && (
                    <div
                        style={{
                            marginTop: "1rem",
                            padding: "1rem",
                            background: "var(--c-eff6ff, #eff6ff)",
                            border: "1px solid var(--c-3b82f6, #3b82f6)",
                            borderRadius: "8px",
                            textAlign: "center",
                            color: "var(--c-1e40af, #1e40af)",
                            fontWeight: 600,
                        }}
                    >
                        ⏳ Otpremam sliku...
                    </div>
                )}

                {uploadedImage && (
                    <div
                        style={{
                            marginTop: "1.5rem",
                            padding: "1.5rem",
                            background: "var(--c-f0fdf4, #f0fdf4)",
                            border: "2px solid var(--c-86efac, #86efac)",
                            borderRadius: "12px",
                        }}
                    >
                        <h3 style={{ fontSize: "1.125rem", fontWeight: 600, color: "var(--c-059669, #059669)", marginBottom: "1rem" }}>
                            ✅ Slika uspešno otpremljena!
                        </h3>
                        
                        <div style={{ marginBottom: "1rem" }}>
                            <p style={{ fontSize: "0.875rem", color: "var(--c-15803d, #15803d)" }}>
                                <strong>Naziv fajla:</strong> {uploadedImage.fileName}
                            </p>
                            <p style={{ fontSize: "0.875rem", color: "var(--c-15803d, #15803d)" }}>
                                <strong>URL:</strong>{" "}
                                <a
                                    href={getImageUrl(uploadedImage.fileName) || "#"}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    style={{ color: "var(--c-2563eb, #2563eb)", textDecoration: "underline" }}
                                >
                                    {getImageUrl(uploadedImage.fileName)}
                                </a>
                            </p>
                            {uploadedImage.productId && (
                                <p style={{ fontSize: "0.875rem", color: "var(--c-15803d, #15803d)" }}>
                                    <strong>ID proizvoda:</strong> {uploadedImage.productId}
                                </p>
                            )}
                        </div>

                        <img
                            src={getImageUrl(uploadedImage.fileName) || ""}
                            alt="Uploaded"
                            style={{
                                maxWidth: "100%",
                                maxHeight: "300px",
                                borderRadius: "8px",
                                border: "1px solid var(--c-d1d5db, #d1d5db)",
                                objectFit: "contain",
                            }}
                        />

                        {uploadedImage.productId && (
                            <button
                                type="button"
                                onClick={handleDeleteImage}
                                style={{
                                    marginTop: "1rem",
                                    background: "linear-gradient(135deg, var(--c-dc2626, #dc2626) 0%, var(--c-b91c1c, #b91c1c) 100%)",
                                    color: "var(--text-on-primary, #ffffff)",
                                    padding: "10px 20px",
                                    borderRadius: "8px",
                                    border: "none",
                                    cursor: "pointer",
                                    fontSize: "0.875rem",
                                    fontWeight: 600,
                                }}
                            >
                                🗑️ Obriši sliku
                            </button>
                        )}
                    </div>
                )}
            </div>

            {/* Usage Instructions */}
            <div
                style={{
                    marginTop: "2rem",
                    padding: "1.5rem",
                    background: "var(--c-f9fafb, #f9fafb)",
                    borderRadius: "12px",
                    border: "1px solid var(--c-e5e7eb, #e5e7eb)",
                }}
            >
                <h3 style={{ fontSize: "1rem", fontWeight: 600, marginBottom: "1rem", color: "var(--c-374151, #374151)" }}>
                    📖 Kako koristiti u drugim komponentama
                </h3>
                <pre
                    style={{
                        background: "var(--c-1f2937, #1f2937)",
                        color: "var(--c-f9fafb, #f9fafb)",
                        padding: "1rem",
                        borderRadius: "8px",
                        fontSize: "0.875rem",
                        overflowX: "auto",
                    }}
                >
{`import ImageUpload from "../components/ImageUpload";
import { uploadImage } from "../services/uploadApi";

const handleImageUpload = async (formData: FormData) => {
    try {
        const result = await uploadImage(formData, productId);
        console.log("Uploaded:", result);
    } catch (error) {
        console.error("Error:", error);
    }
};

<ImageUpload
    onUpload={handleImageUpload}
    label="Slika proizvoda"
    buttonText="📷 Dodaj sliku"
    showPreview={true}
/>`}
                </pre>
            </div>
        </div>
    );
}
