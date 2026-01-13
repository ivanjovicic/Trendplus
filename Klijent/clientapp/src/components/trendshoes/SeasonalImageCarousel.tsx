import { useEffect, useRef, useState } from "react";
import "../../imagecarousel.css";

type ImageItem = {
    id: number;
    imageUrl: string;
};

export default function SeasonalImageCarousel() {
    const [images, setImages] = useState<ImageItem[]>([]);
    const containerRef = useRef<HTMLDivElement>(null);
    const autoScrollRef = useRef<NodeJS.Timeout | null>(null);

    useEffect(() => {
        fetch("/api/trends/seasonal-images")
            .then(r => r.json())
            .then(setImages);
    }, []);

    // Auto scroll
    useEffect(() => {
        startAutoScroll();
        return stopAutoScroll;
    }, [images]);

    const startAutoScroll = () => {
        stopAutoScroll();
        autoScrollRef.current = setInterval(() => {
            scrollBy(200);
        }, 4000); // 👈 menjaš brzinu ovde
    };

    const stopAutoScroll = () => {
        if (autoScrollRef.current) {
            clearInterval(autoScrollRef.current);
            autoScrollRef.current = null;
        }
    };

    const scrollBy = (offset: number) => {
        containerRef.current?.scrollBy({
            left: offset,
            behavior: "smooth"
        });
    };

    if (!images.length) return null;

    return (
        <div style={{ position: "relative", marginTop: 24 }}>
            {/* LEFT */}
            <button
                onClick={() => {
                    stopAutoScroll();
                    scrollBy(-300);
                }}
                className="carousel-btn left"
            >
                ◀
            </button>

            {/* STRIP */}
            <div
                ref={containerRef}
                className="carousel-strip"
                onMouseEnter={stopAutoScroll}
                onMouseLeave={startAutoScroll}
            >
                {images.map(img => (
                    <img
                        key={img.id}
                        src={img.imageUrl}
                        alt="Trend model"
                        loading="lazy"
                        className="carousel-img"
                    />
                ))}
            </div>

            {/* RIGHT */}
            <button
                onClick={() => {
                    stopAutoScroll();
                    scrollBy(300);
                }}
                className="carousel-btn right"
            >
                ▶
            </button>
        </div>
    );
}
