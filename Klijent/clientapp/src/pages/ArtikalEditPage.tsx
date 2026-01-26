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
    const [loadingArtikal, setLoadingArtikal] = React.useState(true);
    const [initialData, setInitialData] = React.useState<ArtikalFormData | null>(null);
    const [error, setError] = React.useState<string | null>(null);
    
    // Image state
    const [currentImagePath, setCurrentImagePath] = React.useState<string | null>(null);

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
                // 1) šifre (tipovi, dobavljači) - Try cache first
                const cachedTipovi = localStorage.getItem("cached_tipovi_obuce");
                const cachedDobavljaci = localStorage.getItem("cached_dobavljaci");

                if (cachedTipovi) setTipovi(JSON.parse(cachedTipovi));
                if (cachedDobavljaci) setDobavljaci(JSON.parse(cachedDobavljaci));

                const fetchLookups = async () => {
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
                            localStorage.setItem("cached_tipovi_obuce", JSON.stringify(tipJson));
                            localStorage.setItem("cached_dobavljaci", JSON.stringify(dobJson));
                        }
                    } catch (e) {
                        console.warn("Lookup fetch failed", e);
                    }
                };

                // 2) artikal
                const fetchArtikalData = async () => {
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
                        idSezona: artikal.idSezona ?? null,
                    };

                    setInitialData(data);
                    setCurrentImagePath(artikal.imagePath ?? null);
                    setLoadingArtikal(false);
                };

                await Promise.all([fetchLookups(), fetchArtikalData()]);

            } catch (e: unknown) {
                if (e instanceof DOMException && e.name === "AbortError") return;
                if (typeof e === "object" && e !== null && "name" in e && (e as { name?: string }).name === "AbortError") return;

                console.error(e);
                const message = e instanceof Error ? e.message : "Greška pri učitavanju artikla.";
                setError(message);
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

    if (loadingArtikal || !initialData) {
        return <div className="card"><p>Učitavanje artikla...</p></div>;
    }

    return (
        <CreateArtikalForm
            tipoviObuce={tipovi}
            dobavljaci={dobavljaci}
            initialData={initialData}
            onSubmit={handleEditSubmit}
            mode="edit"
            artikalId={artikalId}
            currentImagePath={currentImagePath}
            onImageChange={setCurrentImagePath}
        />
    );
}


