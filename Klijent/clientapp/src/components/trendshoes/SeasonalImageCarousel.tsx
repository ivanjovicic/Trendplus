import { useEffect, useRef, useState } from "react";
import "../../imagecarousel.css";
import { makeUrl } from "../../services/analyticsApi";

type ImageItem = {
    id: number;
    imageUrl: string;
    source: string;
    photographerName?: string | null;
    photographerUrl?: string | null;
    sourceUrl?: string | null;
};

export default function SeasonalImageCarousel() {
    const [images, setImages] = useState<ImageItem[]>([]);
    const [selectedImage, setSelectedImage] = useState<ImageItem | null>(null);
    const containerRef = useRef<HTMLDivElement>(null);
    const autoScrollRef = useRef<number | null>(null);

    useEffect(() => {
        const ac = new AbortController();
        void fetch(makeUrl("/api/trends/seasonal-images"), { signal: ac.signal })
            .then(r => {
                if (!r.ok) throw new Error("Failed to load images");
                return r.json();
            })
            .then(data => {
                if (Array.isArray(data)) {
                    setImages(data);
                } else {
                    console.error("Seasonal images response is not an array:", data);
                    setImages([]);
                }
            })
            .catch(err => {
                if ((err as any)?.name === 'AbortError') return;
                console.error("Error loading seasonal images:", err);
                setImages([]);
            });
        return () => ac.abort();
    }, []);

    const startAutoScroll = () => {
        stopAutoScroll();
        autoScrollRef.current = window.setInterval(() => {
            scrollBy(200);
        }, 4000);
    };

    const stopAutoScroll = () => {
        if (autoScrollRef.current) {
            window.clearInterval(autoScrollRef.current);
            autoScrollRef.current = null;
        }
    };

    const scrollBy = (offset: number) => {
        containerRef.current?.scrollBy({
            left: offset,
            behavior: "smooth"
        });
    };

    useEffect(() => {
        if (images.length > 0) {
            startAutoScroll();
        }
        return stopAutoScroll;
    }, [images]);

    const openModal = (img: ImageItem) => {
        setSelectedImage(img);
        stopAutoScroll();
    };

    const closeModal = () => {
        setSelectedImage(null);
        startAutoScroll();
    };

    if (!images.length) return null;

    return (
        <>
            <div style={{ position: "relative", marginTop: 24 }}>
                <button
                    onClick={() => {
                        stopAutoScroll();
                        scrollBy(-300);
                    }}
                    className="carousel-nav-btn left"
                >
                    <svg viewBox="0 0 24 24" fill="none">
                        <polyline points="15 18 9 12 15 6"></polyline>
                    </svg>
                </button>

                <div
                    ref={containerRef}
                    className="carousel-strip"
                    onMouseEnter={stopAutoScroll}
                    onMouseLeave={startAutoScroll}
                >
                    {images.map(img => (
                        <div
                            key={img.id}
                            style={{
                                    position: "relative",
                                    overflow: "hidden",
                                    borderRadius: "10px",
                                    cursor: "pointer",
                                    flexShrink: 0
                                }}
                            onClick={() => openModal(img)}
                            onMouseEnter={(e) => {
                                const overlay = e.currentTarget.querySelector('.zoom-overlay') as HTMLElement;
                                if (overlay) overlay.style.opacity = '1';
                            }}
                            onMouseLeave={(e) => {
                                const overlay = e.currentTarget.querySelector('.zoom-overlay') as HTMLElement;
                                if (overlay) overlay.style.opacity = '0';
                            }}
                        >
                            <img
                                src={img.imageUrl}
                                alt="Trend model"
                                loading="lazy"
                                className="carousel-img"
                            />
                            
                            {/* Zoom Overlay */}
                            <div className="zoom-overlay">
                                <div className="zoom-circle">
                                    <svg 
                                        width="30" 
                                        height="30" 
                                        viewBox="0 0 24 24" 
                                        fill="none" 
                                        stroke="var(--icon-stroke)" 
                                        strokeWidth="2.5" 
                                        strokeLinecap="round" 
                                        strokeLinejoin="round"
                                    >
                                        <polyline points="15 3 21 3 21 9"></polyline>
                                        <polyline points="9 21 3 21 3 15"></polyline>
                                        <line x1="21" y1="3" x2="14" y2="10"></line>
                                        <line x1="3" y1="21" x2="10" y2="14"></line>
                                    </svg>
                                </div>
                            </div>

                            {img.photographerName && (
                                <div className="photographer-badge">📷 {img.photographerName}</div>
                            )}
                        </div>
                    ))}
                </div>

                <button
                    onClick={() => {
                        stopAutoScroll();
                        scrollBy(300);
                    }}
                    className="carousel-nav-btn right"
                >
                    <svg viewBox="0 0 24 24" fill="none">
                        <polyline points="9 18 15 12 9 6"></polyline>
                    </svg>
                </button>
            </div>

            {/* Modal Popup */}
            {selectedImage && (
                <div className="lg:!left-80 carousel-modal-overlay" onClick={closeModal}>
                    <div className="carousel-modal-content">
                        {/* Close Button */}
                        <button onClick={closeModal} className="carousel-close-button">✕</button>

                        {/* Large Image */}
                        <img src={selectedImage.imageUrl} alt="Trend model" className="carousel-modal-image" />

                        {/* Attribution */}
                        {selectedImage.photographerName && (
                            <div className="carousel-modal-attribution">
                                <p style={{ margin: 0, color: 'var(--text-primary-dark, #333)', fontSize: '0.85rem', lineHeight: '1.4' }}>
                                    📷 Photo by{' '}
                                    <a href={selectedImage.photographerUrl || '#'} target="_blank" rel="noopener noreferrer" className="carousel-link">
                                        {selectedImage.photographerName}
                                    </a>
                                    {' '}on{' '}
                                    <a href={selectedImage.sourceUrl || '#'} target="_blank" rel="noopener noreferrer" className="carousel-link">
                                        {selectedImage.source === 'unsplash' ? 'Unsplash' : 'Pexels'}
                                    </a>
                                </p>
                            </div>
                        )}

                        {/* Hint Text */}
                        <p className="carousel-hint-text">Klikni bilo gde da zatvoriš</p>
                    </div>
                </div>
            )}
        </>
    );
}
