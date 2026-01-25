export default function Footer() {
    const currentYear = new Date().getFullYear();

    return (
        <footer style={{
            background: "linear-gradient(135deg, #1f2937 0%, #111827 100%)",
            color: "white",
            marginTop: "4rem",
            borderTop: "3px solid #3b82f6"
        }}>
            <div style={{
                maxWidth: 1200,
                margin: "0 auto",
                padding: "3rem 2rem 2rem"
            }}>
                {/* Main Footer Content */}
                <div style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
                    gap: "2.5rem",
                    marginBottom: "2.5rem"
                }}>
                    {/* Company Info */}
                    <div>
                        <h3 style={{
                            fontSize: "1.5rem",
                            fontWeight: "700",
                            marginBottom: "1rem",
                            background: "linear-gradient(90deg, #3b82f6 0%, #60a5fa 100%)",
                            WebkitBackgroundClip: "text",
                            WebkitTextFillColor: "transparent",
                            backgroundClip: "text"
                        }}>
                            Obuća Trend Plus
                        </h3>
                        <p style={{
                            color: "rgba(255, 255, 255, 0.8)",
                            lineHeight: "1.6",
                            marginBottom: "1rem"
                        }}>
                            Vaša pouzdana prodavnica obuće sa tradicijom i kvalitetom.
                        </p>
                    </div>

                    {/* Location */}
                    <div>
                        <h4 style={{
                            fontSize: "1.1rem",
                            fontWeight: "600",
                            marginBottom: "1rem",
                            color: "#60a5fa"
                        }}>
                            📍 Lokacija
                        </h4>
                        <p style={{
                            color: "rgba(255, 255, 255, 0.8)",
                            lineHeight: "1.8",
                            marginBottom: "0.5rem"
                        }}>
                            <strong>Trgovačka 30B</strong><br />
                            Beograd (Čukarica)<br />
                            Srbija
                        </p>
                        <a
                            href="https://www.google.com/maps/search/?api=1&query=Trgovačka+30B+Beograd+Čukarica"
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                                display: "inline-flex",
                                alignItems: "center",
                                gap: "0.5rem",
                                color: "#60a5fa",
                                textDecoration: "none",
                                fontSize: "0.9rem",
                                marginTop: "0.5rem",
                                transition: "color 0.2s"
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.color = "#93c5fd";
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.color = "#60a5fa";
                            }}
                        >
                            🗺️ Prikaži na mapi
                        </a>
                    </div>

                    {/* Social Media */}
                    <div>
                        <h4 style={{
                            fontSize: "1.1rem",
                            fontWeight: "600",
                            marginBottom: "1rem",
                            color: "#60a5fa"
                        }}>
                            🔗 Pratite nas
                        </h4>
                        <div style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: "0.75rem"
                        }}>
                            {/* Facebook */}
                            <a
                                href="https://www.facebook.com/trendplusobuca/"
                                target="_blank"
                                rel="noopener noreferrer"
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "0.75rem",
                                    padding: "0.75rem 1rem",
                                    background: "rgba(59, 130, 246, 0.1)",
                                    borderRadius: "8px",
                                    color: "white",
                                    textDecoration: "none",
                                    transition: "all 0.3s ease",
                                    border: "1px solid rgba(59, 130, 246, 0.2)"
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.background = "rgba(59, 130, 246, 0.2)";
                                    e.currentTarget.style.transform = "translateX(5px)";
                                    e.currentTarget.style.borderColor = "rgba(59, 130, 246, 0.4)";
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.background = "rgba(59, 130, 246, 0.1)";
                                    e.currentTarget.style.transform = "translateX(0)";
                                    e.currentTarget.style.borderColor = "rgba(59, 130, 246, 0.2)";
                                }}
                            >
                                <span style={{ fontSize: "1.5rem" }}>📘</span>
                                <div>
                                    <div style={{ fontWeight: "600", fontSize: "0.95rem" }}>Facebook</div>
                                    <div style={{
                                        fontSize: "0.8rem",
                                        color: "rgba(255, 255, 255, 0.6)"
                                    }}>
                                        @trendplusobuca
                                    </div>
                                </div>
                            </a>

                            {/* Instagram */}
                            <a
                                href="https://www.instagram.com/trendplusobuca/"
                                target="_blank"
                                rel="noopener noreferrer"
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "0.75rem",
                                    padding: "0.75rem 1rem",
                                    background: "rgba(236, 72, 153, 0.1)",
                                    borderRadius: "8px",
                                    color: "white",
                                    textDecoration: "none",
                                    transition: "all 0.3s ease",
                                    border: "1px solid rgba(236, 72, 153, 0.2)"
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.background = "rgba(236, 72, 153, 0.2)";
                                    e.currentTarget.style.transform = "translateX(5px)";
                                    e.currentTarget.style.borderColor = "rgba(236, 72, 153, 0.4)";
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.background = "rgba(236, 72, 153, 0.1)";
                                    e.currentTarget.style.transform = "translateX(0)";
                                    e.currentTarget.style.borderColor = "rgba(236, 72, 153, 0.2)";
                                }}
                            >
                                <span style={{ fontSize: "1.5rem" }}>📷</span>
                                <div>
                                    <div style={{ fontWeight: "600", fontSize: "0.95rem" }}>Instagram</div>
                                    <div style={{
                                        fontSize: "0.8rem",
                                        color: "rgba(255, 255, 255, 0.6)"
                                    }}>
                                        @trendplusobuca
                                    </div>
                                </div>
                            </a>
                        </div>
                    </div>
                </div>

                {/* Bottom Bar */}
                <div style={{
                    borderTop: "1px solid rgba(255, 255, 255, 0.1)",
                    paddingTop: "1.5rem",
                    display: "flex",
                    flexWrap: "wrap",
                    justifyContent: "space-between",
                    alignItems: "center",
                    gap: "1rem",
                    fontSize: "0.9rem",
                    color: "rgba(255, 255, 255, 0.6)"
                }}>
                    <div>
                        © {currentYear} Obuća Trend Plus. Sva prava zadržana.
                    </div>
                    <div style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "0.5rem"
                    }}>
                        Made with <span style={{ color: "#ef4444" }}>❤️</span> in Belgrade
                    </div>
                </div>
            </div>
        </footer>
    );
}
