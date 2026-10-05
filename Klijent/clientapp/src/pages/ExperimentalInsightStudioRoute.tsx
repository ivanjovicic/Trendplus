import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { isInsightStudioExperimentalEnabled } from "../utils/experimentalFeatures";

export default function ExperimentalInsightStudioRoute({ children }: { children: ReactNode }) {
  if (!isInsightStudioExperimentalEnabled()) {
    return <Navigate to="/analytics" replace />;
  }

  return (
    <>
      <section className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-contrast" role="status">
        <h2 className="font-semibold">Eksperimentalni prikaz — nije sertifikovan</h2>
        <p className="mt-1">
          Podaci mogu biti zastareli, nepotpuni ili imati neproverene identitete. Nemojte koristiti ovaj prikaz kao osnovu za poslovne odluke.
        </p>
      </section>
      {children}
    </>
  );
}
