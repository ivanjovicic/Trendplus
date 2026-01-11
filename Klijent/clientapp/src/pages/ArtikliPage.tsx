import React, { useEffect, useMemo } from "react";
import { useLocation } from "react-router-dom";
import CreateArtikalForm from "../components/CreateArtikalForm";
import UnosArtikalaForm from "../components/UnosArtikalaForm";
import { createArtikal } from "../services/artikliApi";
import { ArtikalFormData } from "../types/artikalformdata";

export default function ArtikliPage() {
  const location = useLocation();
  const [tipovi, setTipovi] = React.useState<{ id: number; naziv: string }[]>([]);
  const [dobavljaci, setDobavljaci] = React.useState<{ id: number; naziv: string }[]>([]);
  const [loadingOptions, setLoadingOptions] = React.useState(true);
  const API = import.meta.env.VITE_API_BASE_URL;

  // Extract state from navigation using useMemo (no setState in effect)
  const navigationState = useMemo(() => {
    const state = location.state as { dobavljacId?: number; dobavljacNaziv?: string; brojRacuna?: string } | null;
    return {
      dobavljacId: state?.dobavljacId ?? null,
      dobavljacNaziv: state?.dobavljacNaziv ?? "",
      brojRacuna: state?.brojRacuna ?? ""
    };
  }, [location.state]);

  // Determine if coming from Unos Robe
  const isUnosRobe = navigationState.dobavljacId !== null && navigationState.brojRacuna !== "";

  React.useEffect(() => {
    let aborted = false;
    const controller = new AbortController();

    const pollOptions = async () => {
      let delay = 1000;
      while (!aborted) {
        try {
          const [tipRes, dobRes] = await Promise.all([
            fetch(`${API}/api/tipovi-obuce`, { signal: controller.signal }),
            fetch(`${API}/api/dobavljaci`, { signal: controller.signal }),
          ]);

          if (tipRes.ok && dobRes.ok) {
            const [tipJson, dobJson] = await Promise.all([tipRes.json(), dobRes.json()]);
            if (aborted) return;
            setTipovi(tipJson ?? []);
            setDobavljaci(dobJson ?? []);
            setLoadingOptions(false);
            return;
          }
        } catch (e) {
          if ((e as any)?.name === "AbortError") return;
        }

        await new Promise((r) => setTimeout(r, delay));
        delay = Math.min(delay * 2, 30000);
      }
    };

    pollOptions();

    return () => {
      aborted = true;
      controller.abort();
    };
  }, [API]);

  const handleSubmit = async (data: ArtikalFormData): Promise<number | void> => {
    const dto = {
      Naziv: data.naziv,
      ProdajnaCena: data.prodajnaCena,
      NabavnaCena: data.nabavnaCena ?? null,
      NabavnaCenaDin: data.nabavnaCenaDin ?? null,
      PrvaProdajnaCena: data.prvaProdajnaCena ?? null,
      Kolicina: data.kolicina ?? null,
      Komentar: data.komentar ?? null,
      tipObuceId: data.tipObuceId ?? null,
      dobavljacId: data.dobavljacId ?? null,
      IDObjekat: null,
      IDSezona: data.idSezona ?? null,
    };

    const id = await createArtikal(dto);
    return id;
  };

  if (loadingOptions) {
    return (
      <div className="card">
        <p style={{ textAlign: 'center', padding: '2rem' }}>Učitavanje...</p>
      </div>
    );
  }

  // Show Unos Artikala Form if coming from Unos Robe
  if (isUnosRobe && navigationState.dobavljacId) {
    return (
      <UnosArtikalaForm
        dobavljacId={navigationState.dobavljacId}
        dobavljacNaziv={navigationState.dobavljacNaziv}
        brojRacuna={navigationState.brojRacuna}
        tipoviObuce={tipovi}
      />
    );
  }

  // Otherwise show regular CreateArtikalForm
  return (
    <CreateArtikalForm
      tipoviObuce={tipovi}
      dobavljaci={dobavljaci}
      onSubmit={handleSubmit}
      initialData={navigationState.dobavljacId ? ({ dobavljacId: navigationState.dobavljacId } as ArtikalFormData) : undefined}
    />
  );
}