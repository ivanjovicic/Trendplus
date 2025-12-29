import React, { useEffect } from "react";
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
  const [initialDobavljacId, setInitialDobavljacId] = React.useState<number | null>(null);
  const [dobavljacNaziv, setDobavljacNaziv] = React.useState<string>("");
  const [brojRacuna, setBrojRacuna] = React.useState<string>("");
  const API = import.meta.env.VITE_API_BASE_URL;

  // Determine if coming from Unos Robe
  const isUnosRobe = location.state && 
    (location.state as any).dobavljacId && 
    (location.state as any).brojRacuna;

  // Extract state from navigation
  useEffect(() => {
    if (location.state) {
      const state = location.state as { dobavljacId?: number; dobavljacNaziv?: string; brojRacuna?: string };
      if (state.dobavljacId) {
        setInitialDobavljacId(state.dobavljacId);
      }
      if (state.dobavljacNaziv) {
        setDobavljacNaziv(state.dobavljacNaziv);
      }
      if (state.brojRacuna) {
        setBrojRacuna(state.brojRacuna);
      }
    }
  }, [location.state]);

  React.useEffect(() => {
    let aborted = false;
    const controller = new AbortController();

    const pollOptions = async () => {
      let delay = 1000;
      while (!aborted) {
        try {
          const [tipRes, dobRes] = await Promise.all([
            fetch(`${API}/tipovi-obuce`, { signal: controller.signal }),
            fetch(`${API}/dobavljaci`, { signal: controller.signal }),
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
      IDSezona: null,
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
  if (isUnosRobe && initialDobavljacId && brojRacuna) {
    return (
      <UnosArtikalaForm
        dobavljacId={initialDobavljacId}
        dobavljacNaziv={dobavljacNaziv}
        brojRacuna={brojRacuna}
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
      loadingOptions={loadingOptions}
      initialData={initialDobavljacId ? { dobavljacId: initialDobavljacId } as ArtikalFormData : undefined}
    />
  );
}