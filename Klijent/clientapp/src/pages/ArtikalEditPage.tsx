import React from "react";
import { useParams } from "react-router-dom";
import CreateArtikalForm from "../components/CreateArtikalForm";
import { ArtikalFormData } from "../types/artikalformdata";
import { getArtikal, updateArtikal } from "../services/artikliApi";

export default function ArtikalEditPage() {
    const { id } = useParams<{ id: string }>();
    const artikalId = Number(id);

    const [tipovi, setTipovi] = React.useState<{ id: number; naziv: string }[]>([]);
    const [dobavljaci, setDobavljaci] = React.useState<{ id: number; naziv: string }[]>([]);
    const [loadingOptions, setLoadingOptions] = React.useState(true);
    const [loadingArtikal, setLoadingArtikal] = React.useState(true);
    const [initialData, setInitialData] = React.useState<ArtikalFormData | null>(null);
    const [error, setError] = React.useState<string | null>(null);

    const API = import.meta.env.VITE_API_BASE_URL;

    React.useEffect(() => {
        if (!artikalId || Number.isNaN(artikalId)) {
            setError("Neispravan ID artikla.");
            return;
        }

        let aborted = false;
        const controller = new AbortController();

        const load = async () => {
            try {
                // 1) šifre (tipovi, dobavljači)
                const [tipRes, dobRes] = await Promise.all([
                    fetch(`${API}/tipovi-obuce`, { signal: controller.signal }),
                    fetch(`${API}/dobavljaci`, { signal: controller.signal }),
                ]);

                if (!tipRes.ok || !dobRes.ok) {
                    throw new Error("Ne mogu da učitam šifre.");
                }

                const [tipJson, dobJson] = await Promise.all([tipRes.json(), dobRes.json()]);
                if (aborted) return;

                setTipovi(tipJson ?? []);
                setDobavljaci(dobJson ?? []);
                setLoadingOptions(false);

                // 2) artikal
                const artikal = await getArtikal(artikalId);
                if (aborted) return;

                const data: ArtikalFormData = {
                    naziv: artikal.naziv,
                    prodajnaCena: artikal.prodajnaCena,
                    nabavnaCena: artikal.nabavnaCena ?? null,
                    nabavnaCenaDin: artikal.nabavnaCenaDin ?? null,
                    prvaProdajnaCena: artikal.prvaProdajnaCena ?? null,
                    kolicina: artikal.kolicina ?? null,
                    komentar: artikal.komentar ?? null,
                    tipObuceId: artikal.tipObuceId ?? null,
                    dobavljacId: artikal.dobavljacId ?? null,
                };

                setInitialData(data);
                setLoadingArtikal(false);
            } catch (e: any) {
                if (e?.name === "AbortError") return;
                console.error(e);
                setError(e?.message ?? "Greška pri učitavanju artikla.");
                setLoadingArtikal(false);
            }
        };

        load();

        return () => {
            aborted = true;
            controller.abort();
        };
    }, [API, artikalId]);

    const handleEditSubmit = async (data: ArtikalFormData): Promise<void> => {
        if (!artikalId || Number.isNaN(artikalId)) {
            throw new Error("Neispravan ID artikla.");
        }
        await updateArtikal(artikalId, data);
    };

    if (error) {
        return <div className="card"><p className="error-msg">{error}</p></div>;
    }

    if (loadingOptions || loadingArtikal || !initialData) {
        return <div className="card"><p>Učitavanje artikla...</p></div>;
    }

    return (
        <CreateArtikalForm
            tipoviObuce={tipovi}
            dobavljaci={dobavljaci}
            initialData={initialData}
            onSubmit={handleEditSubmit}
            loadingOptions={false}
            mode="edit"
        />
    );
}


