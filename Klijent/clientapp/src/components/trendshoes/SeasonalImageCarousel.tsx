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
        fetch(makeUrl("/api/trends/seasonal-images"))
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
                console.error("Error loading seasonal images:", err);
                setImages([]);
            });
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
                            <div 
                                className="zoom-overlay"
                                style={{
                                    position: "absolute",
                                    top: 0,
                                    left: 0,
                                    right: 0,
                                    bottom: 0,
                                    background: "rgba(0, 0, 0, 0.5)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    opacity: 0,
                                    transition: "opacity 0.3s ease",
                                    pointerEvents: "none"
                                }}
                            >
                                <div style={{
                                    width: "60px",
                                    height: "60px",
                                    borderRadius: "50%",
                                    background: "rgba(255, 255, 255, 0.95)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    boxShadow: "0 4px 20px rgba(0, 0, 0, 0.3)",
                                    border: "2px solid rgba(255, 255, 255, 0.5)"
                                }}>
                                    <svg 
                                        width="30" 
                                        height="30" 
                                        viewBox="0 0 24 24" 
                                        fill="none" 
                                        stroke="#374151" 
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
                                <div style={{
                                    position: "absolute",
                                    bottom: 8,
                                    left: 8,
                                    background: "rgba(0,0,0,0.75)",
                                    color: "white",
                                    padding: "4px 10px",
                                    borderRadius: 6,
                                    fontSize: "0.7rem",
                                    lineHeight: "1.2",
                                    zIndex: 10,
                                    pointerEvents: "none",
                                    backdropFilter: "blur(4px)"
                                }}>
                                    📷 {img.photographerName}
                                </div>
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
                <div
                    style={{
                        position: "fixed",
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        backgroundColor: "rgba(0, 0, 0, 0.9)",
                        zIndex: 9999,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        padding: "2rem",
                        cursor: "pointer"
                    }}
                    onClick={closeModal}
                >
                    <div
                        style={{
                            position: "relative",
                            width: "100%",
                            height: "100%",
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            justifyContent: "center",
                            gap: "1rem"
                        }}
                    >
                        {/* Close Button */}
                        <button
                            onClick={closeModal}
                            style={{
                                position: "absolute",
                                top: 10,
                                right: 10,
                                background: "rgba(255, 255, 255, 0.9)",
                                border: "none",
                                borderRadius: "50%",
                                width: 40,
                                height: 40,
                                fontSize: "1.5rem",
                                cursor: "pointer",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                fontWeight: "bold",
                                color: "#333",
                                transition: "all 0.2s",
                                zIndex: 10000
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.background = "#fff";
                                e.currentTarget.style.transform = "scale(1.1)";
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.background = "rgba(255, 255, 255, 0.9)";
                                e.currentTarget.style.transform = "scale(1)";
                            }}
                        >
                            ✕
                        </button>

                        {/* Large Image */}
                        <img
                            src={selectedImage.imageUrl}
                            alt="Trend model"
                            style={{
                                maxWidth: "85vw",
                                maxHeight: "75vh",
                                width: "auto",
                                height: "auto",
                                objectFit: "contain",
                                borderRadius: "8px",
                                boxShadow: "0 20px 60px rgba(0,0,0,0.5)"
                            }}
                        />

                        {/* Attribution */}
                        {selectedImage.photographerName && (
                            <div style={{
                                background: "rgba(255, 255, 255, 0.95)",
                                padding: "0.75rem 1.5rem",
                                borderRadius: "8px",
                                textAlign: "center",
                                maxWidth: "600px"
                            }}>
                                <p style={{
                                    margin: 0,
                                    color: "#333",
                                    fontSize: "0.85rem",
                                    lineHeight: "1.4"
                                }}>
                                    📷 Photo by{" "}
                                    <a
                                        href={selectedImage.photographerUrl || "#"}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        style={{
                                            color: "#2563eb",
                                            fontWeight: "600",
                                            textDecoration: "none"
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.textDecoration = "underline";
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.textDecoration = "none";
                                        }}
                                    >
                                        {selectedImage.photographerName}
                                    </a>
                                    {" "}on{" "}
                                    <a
                                        href={selectedImage.sourceUrl || "#"}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        style={{
                                            color: "#2563eb",
                                            fontWeight: "600",
                                            textDecoration: "none"
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.textDecoration = "underline";
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.textDecoration = "none";
                                        }}
                                    >
                                        {selectedImage.source === "unsplash" ? "Unsplash" : "Pexels"}
                                    </a>
                                </p>
                            </div>
                        )}

                        {/* Hint Text */}
                        <p style={{
                            margin: 0,
                            color: "rgba(255, 255, 255, 0.7)",
                            fontSize: "0.8rem",
                            textAlign: "center"
                        }}>
                            Klikni bilo gde da zatvoriš
                        </p>
                    </div>
                </div>
            )}
        </>
    );
}
