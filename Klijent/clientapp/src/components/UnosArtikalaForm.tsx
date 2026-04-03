import { useState, useEffect } from "react";
import { createArtikal, getArtikli } from "../services/artikliApi";
import { getSezone } from "../services/sezoneApi";
import { Artikal } from "../types/Artikal";
import type { Sezona } from "../types/Sezona";

interface ArtikalStavka {
    id?: number;
    naziv: string;
    kolicina: number;
    nabavnaCena: number;
    prodajnaCena: number;
    tipObuceId: number | null;
    sezonaId: number | null;
    komentar: string;
    isExisting: boolean;
}

interface UnosArtikalaFormProps {
    dobavljacId: number;
    dobavljacNaziv: string;
    brojRacuna: string;
    tipoviObuce: { id: number; naziv: string }[];
}

export default function UnosArtikalaForm({ 
    dobavljacId, 
    dobavljacNaziv, 
    brojRacuna, 
    tipoviObuce 
}: UnosArtikalaFormProps) {
    const [sezone, setSezone] = useState<Sezona[]>([]);
    const [stavke, setStavke] = useState<ArtikalStavka[]>([
        {
            naziv: "",
            kolicina: 1,
            nabavnaCena: 0,
            prodajnaCena: 0,
            tipObuceId: null,
            sezonaId: null,
            komentar: "",
            isExisting: false
        }
    ]);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [successCount, setSuccessCount] = useState(0);
    
    const [artiklList, setArtiklList] = useState<Artikal[]>([]);
    const [searchQuery, setSearchQuery] = useState<{ [key: number]: string }>({});
    const [showDropdown, setShowDropdown] = useState<{ [key: number]: boolean }>({});

    const [fontSize, setFontSize] = useState('0.875rem');
    const [inputPadding, setInputPadding] = useState('10px');

    useState(() => {
        const handleResize = () => {
            const width = window.innerWidth;
            if (width < 1024) {
                setFontSize('0.75rem');
                setInputPadding('8px');
            } else if (width < 1366) {
                setFontSize('0.8125rem');
                setInputPadding('9px');
            } else {
                setFontSize('0.875rem');
                setInputPadding('10px');
            }
        };

        handleResize();
        window.addEventListener('resize', handleResize);
        return () => window.removeEventListener('resize', handleResize);
    });

    useState(() => {
        const loadArtikli = async () => {
            try {
                const data = await getArtikli();
                setArtiklList(data);
            } catch (err) {
                console.error("Failed to load artikli", err);
            }
        };
        loadArtikli();
    });

    useEffect(() => {
        const loadSezone = async () => {
            try {
                const data = await getSezone();
                setSezone(data);
            } catch (err) {
                console.error("Failed to load sezone:", err);
            }
        };
        loadSezone();
    }, []);

    const getFilteredArtikli = (rowIndex: number): Artikal[] => {
        const query = searchQuery[rowIndex]?.toLowerCase() || "";
        if (!query.trim()) return [];
        
        return artiklList
            .filter(a => a.naziv.toLowerCase().includes(query))
            .slice(0, 10);
    };

    const addStavka = () => {
        setStavke(prev => [
            ...prev,
            {
                naziv: "",
                kolicina: 1,
                nabavnaCena: 0,
                prodajnaCena: 0,
                tipObuceId: null,
                sezonaId: null,
                komentar: "",
                isExisting: false
            }
        ]);
    };

    const removeStavka = (index: number) => {
        if (stavke.length > 1) {
            setStavke(prev => prev.filter((_, i) => i !== index));
            const newSearchQuery = { ...searchQuery };
            const newShowDropdown = { ...showDropdown };
            delete newSearchQuery[index];
            delete newShowDropdown[index];
            setSearchQuery(newSearchQuery);
            setShowDropdown(newShowDropdown);
        }
    };

    const selectExistingArtikal = (rowIndex: number, artikal: Artikal) => {
        setStavke(prev => {
            const updated = [...prev];
            updated[rowIndex] = {
                id: artikal.id,
                naziv: artikal.naziv,
                kolicina: 1,
                nabavnaCena: artikal.nabavnaCena || 0,
                prodajnaCena: artikal.prodajnaCena,
                tipObuceId: null,
                sezonaId: null,
                komentar: "",
                isExisting: true
            };
            return updated;
        });
        
        setSearchQuery(prev => ({ ...prev, [rowIndex]: artikal.naziv }));
        setShowDropdown(prev => ({ ...prev, [rowIndex]: false }));
    };

    const shouldOpenUpward = (rowIndex: number): boolean => {
        return rowIndex >= stavke.length - 2 && stavke.length > 3;
    };

    const updateStavka = (index: number, field: keyof ArtikalStavka, value: unknown) => {
        setStavke(prev => {
            const updated = [...prev];
            updated[index] = { ...updated[index], [field]: value };
            
            if (field === 'naziv') {
                updated[index].isExisting = false;
                updated[index].id = undefined;
            }
            
            return updated;
        });
    };

    const handleSearchChange = (rowIndex: number, value: string) => {
        setSearchQuery(prev => ({ ...prev, [rowIndex]: value }));
        setShowDropdown(prev => ({ ...prev, [rowIndex]: true }));
        updateStavka(rowIndex, 'naziv', value);
    };

    const handleSubmitAll = async () => {
        setError(null);
        setSuccessCount(0);

        const hasEmptyNaziv = stavke.some(s => !s.naziv.trim());
        if (hasEmptyNaziv) {
            setError("Svi artikli moraju imati naziv");
            return;
        }

        setIsSubmitting(true);
        let successfulCount = 0;
        const ukupanIznos = stavke.reduce((sum, s) => sum + (s.kolicina * s.nabavnaCena), 0);

        try {
            for (const stavka of stavke) {
                if (stavka.isExisting && stavka.id) {
                    console.log(`Updating existing article ID ${stavka.id} with +${stavka.kolicina}`);
                } else {
                    const dto = {
                        Naziv: stavka.naziv,
                        ProdajnaCena: stavka.prodajnaCena,
                        NabavnaCena: stavka.nabavnaCena,
                        NabavnaCenaDin: null,
                        PrvaProdajnaCena: null,
                        Kolicina: stavka.kolicina,
                        Komentar: stavka.komentar || `Unos robe - Račun: ${brojRacuna}`,
                        tipObuceId: stavka.tipObuceId,
                        dobavljacId: dobavljacId,
                        IDObjekat: null,
                        IDSezona: stavka.sezonaId,
                    };

                    await createArtikal(dto);
                }
                
                successfulCount++;
                setSuccessCount(successfulCount);
            }

            console.log("Creating DnevnikPromena entry:", {
                tipPromene: "Unos robe",
                datum: new Date().toISOString(),
                iznos: ukupanIznos,
                brojRacuna: brojRacuna,
                dobavljacId: dobavljacId
            });

            alert(`Uspešno obrađeno ${successfulCount} artikala!\nUkupan iznos: ${ukupanIznos.toFixed(2)} RSD`);
            setStavke([{
                naziv: "",
                kolicina: 1,
                nabavnaCena: 0,
                prodajnaCena: 0,
                tipObuceId: null,
                sezonaId: null,
                komentar: "",
                isExisting: false
            }]);
            setSuccessCount(0);
            setSearchQuery({});
            setShowDropdown({});
        } catch (err) {
            setError((err as Error)?.message || "Greška pri unosu artikala");
        } finally {
            setIsSubmitting(false);
        }
    };

    const ukupnoStavki = stavke.length;
    const ukupnaVrednost = stavke.reduce((sum, s) => sum + (s.kolicina * s.nabavnaCena), 0);
    const novihArtikala = stavke.filter(s => !s.isExisting).length;
    const postojecihArtikala = stavke.filter(s => s.isExisting).length;

    return (
        <div className="card w-full">
           {/*     <h2 className="text-2xl font-semibold mb-6">Unos artikala</h2>*/}

                <div className="rounded-lg border-2 p-4 mb-5 bg-info-10" style={{ borderColor: 'var(--info)' }}>
                    <h2 className="font-semibold text-base mb-2 text-info">
                        Unos robe
                    </h2>
                    <div className="text-sm text-info">
                        <p>Broj računa: <strong>{brojRacuna}</strong></p>
                        <p>Dobavljač: <strong>{dobavljacNaziv}</strong> (ID: {dobavljacId})</p>
                        <p className="mt-2 text-muted">Tip: Pretražite postojeće artikle ili unesite novi naziv</p>
                    </div>
                </div>

            <div className="mb-4">
                <h3 className="font-semibold text-lg mb-3">Lista artikala ({ukupnoStavki} - {novihArtikala} novih, {postojecihArtikala} postojećih)</h3>
            </div>

            <div className="overflow-x-auto mb-6 w-full min-h-[500px]">
                <table style={{
                    width: '100%',
                    borderCollapse: 'collapse',
                    fontSize: fontSize,
                    minWidth: '1200px'
                }}>
                    <thead>
                        <tr style={{
                            background: 'var(--surface-elevated)'
                        }}>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, width: '20%', minWidth: '200px' }}>
                                Naziv artikla (pretraga)
                            </th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, width: '12%', minWidth: '120px' }}>
                                Tip obuće
                            </th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, width: '13%', minWidth: '130px' }}>
                                Sezona
                            </th>
                            <th style={{ padding: '12px', textAlign: 'center', fontWeight: 600, width: '8%', minWidth: '90px' }}>
                                Kolicina
                            </th>
                            <th style={{ padding: '12px', textAlign: 'right', fontWeight: 600, width: '11%', minWidth: '110px' }}>
                                Nabavna cena
                            </th>
                            <th style={{ padding: '12px', textAlign: 'right', fontWeight: 600, width: '11%', minWidth: '110px' }}>
                                Prodajna cena
                            </th>
                            <th style={{ padding: '12px', textAlign: 'left', fontWeight: 600, width: '18%', minWidth: '140px' }}>
                                Komentar
                            </th>
                            <th style={{ padding: '12px', textAlign: 'center', fontWeight: 600, width: '7%', minWidth: '80px' }}>
                                Akcija
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {stavke.map((stavka, index) => {
                            const filteredArtikli = getFilteredArtikli(index);
                            const showDropdownForRow = showDropdown[index] && filteredArtikli.length > 0;
                            const openUpward = shouldOpenUpward(index);

                            return (
                                <tr key={index} style={{
                                            borderBottom: '1px solid var(--border-default)',
                                            background: stavka.isExisting ? 'var(--success-10)' : (index % 2 === 0 ? 'var(--surface-default)' : 'var(--surface-light)'),
                                            height: '70px'
                                        }}>
                                    <td style={{ padding: '8px', position: 'relative' }}>
                                        <input
                                            type="text"
                                            placeholder="Unesite novi ili pretražite postojeće..."
                                            value={searchQuery[index] || stavka.naziv}
                                            onChange={(e) => handleSearchChange(index, e.target.value)}
                                            onFocus={() => setShowDropdown(prev => ({ ...prev, [index]: true }))}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                background: stavka.isExisting ? 'var(--success-10)' : 'var(--surface-elevated)',
                                                borderColor: stavka.isExisting ? 'var(--success)' : undefined,
                                                boxShadow: stavka.isExisting ? undefined : 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))',
                                                width: '100%'
                                            }}
                                        />
                                        
                                        {showDropdownForRow && (
                                            <div style={{
                                                position: 'absolute',
                                                ...(openUpward ? { bottom: '100%', marginBottom: '4px' } : { top: '100%', marginTop: '4px' }),
                                                left: '8px',
                                                right: '8px',
                                                background: 'var(--surface-default)',
                                                border: '2px solid var(--info)',
                                                borderRadius: '8px',
                                                maxHeight: '400px',
                                                overflowY: 'auto',
                                                zIndex: 1500,
                                                boxShadow: 'var(--box-shadow-lg, 0 10px 20px rgba(0, 0, 0, 0.2))',
                                                minWidth: '300px',
                                            }}>
                                                {filteredArtikli.map((art) => (
                                                            <div
                                                                key={art.id}
                                                                onClick={() => selectExistingArtikal(index, art)}
                                                                className="p-3 cursor-pointer border-b"
                                                                style={{ fontSize: fontSize, transition: 'background 0.15s' }}
                                                                onMouseEnter={(e) => { (e.currentTarget as HTMLDivElement).style.background = 'var(--info-10)'; }}
                                                                onMouseLeave={(e) => { (e.currentTarget as HTMLDivElement).style.background = 'var(--surface-default)'; }}
                                                            >
                                                                <div className="font-semibold mb-1">{art.naziv}</div>
                                                                <div className="text-sm text-muted mt-1">
                                                                    Cena: {art.prodajnaCena} RSD | Kolicina: {art.kolicina || 0}
                                                                </div>
                                                            </div>
                                                ))}
                                            </div>
                                        )}
                                        
                                        {stavka.isExisting && (
                                            <div style={{ fontSize: '0.75rem', color: 'var(--success)', marginTop: '4px' }}>
                                                [Postojeći artikal - ID: {stavka.id}]
                                            </div>
                                        )}
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                        <select
                                            value={stavka.tipObuceId ?? ""}
                                            onChange={(e) => updateStavka(index, 'tipObuceId', e.target.value ? Number(e.target.value) : null)}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                width: '100%',
                                                background: 'var(--surface-elevated)',
                                                boxShadow: 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))'
                                            }}
                                        >
                                            <option value="">-- izaberite --</option>
                                            {tipoviObuce.map(t => (
                                                <option key={t.id} value={t.id}>{t.naziv}</option>
                                            ))}
                                        </select>
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                        <select
                                            value={stavka.sezonaId ?? ""}
                                            onChange={(e) => updateStavka(index, 'sezonaId', e.target.value ? Number(e.target.value) : null)}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                width: '100%',
                                                background: 'var(--surface-elevated)',
                                                boxShadow: 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))'
                                            }}
                                        >
                                            <option value="">-- izaberite --</option>
                                            {sezone.map(s => (
                                                <option key={s.id} value={s.id}>{s.naziv}</option>
                                            ))}
                                        </select>
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                        <input
                                            type="number"
                                            min={1}
                                            value={stavka.kolicina}
                                            onChange={(e) => updateStavka(index, 'kolicina', Number(e.target.value))}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                textAlign: 'center',
                                                width: '100%',
                                                background: 'var(--surface-elevated)',
                                                boxShadow: 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))'
                                            }}
                                        />
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                        <input
                                            type="number"
                                            min={0}
                                            step={0.01}
                                            value={stavka.nabavnaCena}
                                            onChange={(e) => updateStavka(index, 'nabavnaCena', Number(e.target.value))}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                textAlign: 'right',
                                                width: '100%',
                                                background: 'var(--surface-elevated)',
                                                boxShadow: 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))'
                                            }}
                                        />
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                        <input
                                            type="number"
                                            min={0}
                                            step={0.01}
                                            value={stavka.prodajnaCena}
                                            onChange={(e) => updateStavka(index, 'prodajnaCena', Number(e.target.value))}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                textAlign: 'right',
                                                width: '100%',
                                                background: 'var(--surface-elevated)',
                                                boxShadow: 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))'
                                            }}
                                        />
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                        <input
                                            type="text"
                                            placeholder="Opciono..."
                                            value={stavka.komentar}
                                            onChange={(e) => updateStavka(index, 'komentar', e.target.value)}
                                            className="input-big placeholder:text-muted"
                                            style={{
                                                marginBottom: 0,
                                                padding: inputPadding,
                                                fontSize: fontSize,
                                                width: '100%',
                                                background: 'var(--surface-elevated)',
                                                boxShadow: 'var(--box-shadow-sm, 0 1px 2px rgba(0,0,0,0.04))'
                                            }}
                                        />
                                    </td>
                                    <td style={{ padding: '8px', textAlign: 'center' }}>
                                        <button
                                            onClick={() => removeStavka(index)}
                                            disabled={stavke.length === 1}
                                            className="rounded px-3 py-2 text-sm font-semibold"
                                            style={{
                                                background: stavke.length === 1 ? 'var(--muted, #9ca3af)' : 'var(--danger)',
                                                color: 'var(--on-danger, white)',
                                                padding: '8px 14px',
                                                borderRadius: '6px',
                                                border: 'none',
                                                cursor: stavke.length === 1 ? 'not-allowed' : 'pointer',
                                                fontSize: '0.875rem',
                                                fontWeight: 600,
                                                minWidth: '60px'
                                            }}
                                        >
                                            X
                                        </button>
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>

            <button
                onClick={addStavka}
                className="button-big bg-success text-on-success"
                style={{
                    maxWidth: '200px',
                    marginBottom: '1.5rem'
                }}
            >
                + Dodaj artikal
            </button>

            <div style={{
                borderTop: '2px solid var(--border-default)',
                paddingTop: '1rem',
                marginBottom: '1rem',
            }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                    <span style={{ fontWeight: 600 }}>Ukupno artikala:</span>
                    <span style={{ fontWeight: 600, color: 'var(--success)' }}>
                        {ukupnoStavki} ({novihArtikala} novih + {postojecihArtikala} postojećih)
                    </span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ fontWeight: 600 }}>Ukupna vrednost (nabavna):</span>
                    <span style={{ fontWeight: 600, fontSize: '1.125rem', color: 'var(--success)' }}>
                        {ukupnaVrednost.toFixed(2)} RSD
                    </span>
                </div>
            </div>

            <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                <button
                    onClick={handleSubmitAll}
                    disabled={isSubmitting}
                    className="button-big bg-primary text-on-primary"
                    style={{
                        maxWidth: '300px'
                    }}
                >
                    {isSubmitting 
                        ? `Unosim... (${successCount}/${ukupnoStavki})` 
                        : `Sačuvaj sve artikle (${ukupnoStavki})`
                    }
                </button>

                {isSubmitting && (
                    <span style={{ fontSize: '0.875rem', color: 'var(--muted)' }}>
                        Molimo sačekajte...
                    </span>
                )}
            </div>

            {error && (
                <p className="error-msg" style={{ marginTop: '1rem' }}>
                    {error}
                </p>
            )}
        </div>
    );
}
