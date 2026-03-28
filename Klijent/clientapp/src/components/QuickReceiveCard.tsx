import React from 'react';
import '../styles/themes.css';

export default function QuickReceiveCard() {
  return (
    <section className="rounded-2xl border p-4 sm:p-5 card-theme">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="rounded-lg p-2 card-icon" aria-hidden>
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-clipboard-plus" aria-hidden="true"><rect width="8" height="4" x="8" y="2" rx="1" ry="1"></rect><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"></path><path d="M9 14h6"></path><path d="M12 17v-6"></path></svg>
            </span>
            <h1 className="text-lg font-semibold sm:text-xl card-title">Unos robe</h1>
          </div>
          <p className="mt-2 max-w-3xl text-sm card-desc">
            Brzi prijem robe: racun + dobavljac + nastavak na stavke, sa fokusom na sto manje klikova.
          </p>
        </div>
      </div>
    </section>
  );
}
