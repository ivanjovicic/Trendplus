import React from "react";
import CreateArtikalForm from "../components/CreateArtikalForm";
import { createArtikal } from "../services/artikliApi";
import { ArtikalFormData } from "../types/artikalformdata";

export default function ArtikliPage() {
  const [tipovi, setTipovi] = React.useState<{ id: number; naziv: string }[]>([]);
  const [dobavljaci, setDobavljaci] = React.useState<{ id: number; naziv: string }[]>([]);
  const [loadingOptions, setLoadingOptions] = React.useState(true);
  const API = import.meta.env.VITE_API_BASE_URL;

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

  return (
    <CreateArtikalForm
      tipoviObuce={tipovi}
      dobavljaci={dobavljaci}
      onSubmit={handleSubmit}
      loadingOptions={loadingOptions}
    />
  );
}