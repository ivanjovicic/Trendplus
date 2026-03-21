export default function DashboardFooter() {
  const currentYear = new Date().getFullYear();

    return (
    <footer className="mt-8 border-t border-border bg-surface px-4 py-6">
      <div className="mx-auto grid w-full max-w-[1320px] gap-6 md:grid-cols-3">
        <div>
          <h3 className="text-lg font-semibold text-foreground">Obuća Trend Plus</h3>
          <p className="mt-2 text-sm text-muted">Vaša pouzdana prodavnica obuće sa tradicijom i kvalitetom.</p>
        </div>
        <div>
          <h4 className="text-sm font-semibold uppercase tracking-wide text-muted">Lokacija</h4>
          <p className="mt-2 text-sm text-muted">Trgovačka 30B, Beograd (Čukarica), Srbija</p>
          <a
            className="mt-2 inline-block text-sm text-primary hover:text-primary/90"
            href="https://www.google.com/maps/search/?api=1&query=Trgovačka+30B+Beograd+Čukarica"
            target="_blank"
            rel="noopener noreferrer"
          >
            Prikaži na mapi
          </a>
        </div>
        <div>
          <h4 className="text-sm font-semibold uppercase tracking-wide text-muted">Pratite nas</h4>
          <p className="mt-2 text-sm text-muted">Facebook @trendplusobuca</p>
          <p className="text-sm text-muted">Instagram @trendplusobuca</p>
        </div>
      </div>
      <div className="mx-auto mt-6 flex w-full max-w-[1320px] flex-wrap items-center justify-between gap-2 border-t border-border pt-4 text-xs text-muted">
        <span>© {currentYear} Obuća Trend Plus. Sva prava zadržana.</span>
        <span>Made with love in Belgrade</span>
      </div>
    </footer>
  );
}

