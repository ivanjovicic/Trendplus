import { useEffect, useState } from "react";

type SeasonalImage = {
    id: number;
    imageUrl: string;
    title?: string;
};

export default function SeasonalImageStrip() {
    const [images, setImages] = useState<SeasonalImage[]>([]);

    useEffect(() => {
        fetch('/api/trends/seasonal-images')
            .then(async r => {
                const text = await r.text();
                console.log('RAW RESPONSE:', text);
                return JSON.parse(text);
            })
            .then(setImages)
            .catch(console.error);
    }, []);

    if (!images.length) return null;

    return (
        <div
            style={{
                marginTop: 24,
                padding: "12px 0",
                borderTop: "1px solid #e5e7eb",
                display: "flex",
                gap: 12,
                overflowX: "auto",
            }}
        >
            {images.map(img => (
                <img
                    src={img.imageUrl}
                    alt="Trend model"
                    style={{
                        height: 140,       
                        width: "auto",
                        maxWidth: 220,
                        objectFit: "contain",
                        borderRadius: 10
                    }}
                />
            ))}
        </div>
    );
}
