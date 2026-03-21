export default function Footer() {
    const currentYear = new Date().getFullYear();

    return (
        <footer className="mt-16 bg-gradient-to-br from-gray-800 to-gray-900 text-white border-t-4 border-primary">
            <div className="max-w-screen-lg mx-auto px-6 py-12">
                {/* Main Footer Content */}
                <div className="grid gap-10 grid-cols-[repeat(auto-fit,minmax(280px,1fr))] mb-10">
                    {/* Company Info */}
                    <div>
                        <h3 className="text-2xl font-extrabold mb-4 bg-gradient-to-r from-blue-500 to-sky-400 bg-clip-text text-transparent">Obuća Trend Plus</h3>
                        <p className="text-white/85 leading-loose mb-4">Vaša pouzdana prodavnica obuće sa tradicijom i kvalitetom.</p>
                    </div>

                    {/* Location */}
                    <div>
                        <h4 className="text-lg font-semibold mb-3 text-primary">📍 Lokacija</h4>
                        <p className="text-white/80 leading-relaxed mb-2"><strong>Trgovačka 30B</strong><br/>Beograd (Čukarica)<br/>Srbija</p>
                        <a href="https://www.google.com/maps/search/?api=1&query=Trgovačka+30B+Beograd+Čukarica" target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-2 text-primary no-underline text-sm hover:text-primary-300 transition">🗺️ Prikaži na mapi</a>
                    </div>

                    {/* Social Media */}
                    <div>
                        <h4 className="text-lg font-semibold mb-3 text-primary">🔗 Pratite nas</h4>
                        <div className="flex flex-col gap-3">
                            {/* Facebook */}
                            <a href="https://www.facebook.com/trendplusobuca/" target="_blank" rel="noopener noreferrer" className="flex items-center gap-3 p-3 rounded-lg bg-primary/10 border border-primary/20 text-white no-underline hover:bg-primary/20 hover:translate-x-1 transition">
                                <span className="text-xl">📘</span>
                                <div>
                                    <div className="font-semibold text-sm">Facebook</div>
                                    <div className="text-xs text-white/70">@trendplusobuca</div>
                                </div>
                            </a>

                            {/* Instagram */}
                            <a href="https://www.instagram.com/trendplusobuca/" target="_blank" rel="noopener noreferrer" className="flex items-center gap-3 p-3 rounded-lg bg-pink-600/10 border border-pink-600/20 text-white no-underline hover:bg-pink-600/20 hover:translate-x-1 transition">
                                <span className="text-xl">📷</span>
                                <div>
                                    <div className="font-semibold text-sm">Instagram</div>
                                    <div className="text-xs text-white/70">@trendplusobuca</div>
                                </div>
                            </a>
                        </div>
                    </div>
                </div>

                {/* Bottom Bar */}
                <div className="pt-6 border-t border-white/10 flex flex-wrap justify-between items-center gap-4 text-sm text-white/70">
                    <div>© {currentYear} Obuća Trend Plus. Sva prava zadržana.</div>
                    <div className="flex items-center gap-2">Made with <span className="text-accent-error">❤️</span> in Belgrade</div>
                </div>
            </div>
        </footer>
    );
}
