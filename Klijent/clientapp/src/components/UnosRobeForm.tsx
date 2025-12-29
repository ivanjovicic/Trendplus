import { useState, useMemo, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
}

interface UnosRobeFormProps {
    dobavljaci: Dobavljac[];
    onSubmit: (data: { dobavljacId: number; brojRacuna: string; artikli: unknown[] }) => Promise<void>;
}

export default function UnosRobeForm({ dobavljaci }: UnosRobeFormProps) {
    const navigate = useNavigate();
    const [selectedDobavljac, setSelectedDobavljac] = useState<Dobavljac | null>(null);
    const [brojRacuna, setBrojRacuna] = useState("");
    const [searchQuery, setSearchQuery] = useState("");
    const [showSearchResults, setShowSearchResults] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const searchRef = useRef<HTMLDivElement>(null);

    const filteredDobavljaci = useMemo(() => {
        if (!searchQuery.trim()) return dobavljaci;
        
        const query = searchQuery.toLowerCase();
        return dobavljaci
            .filter(d => 
                d.naziv.toLowerCase().includes(query) ||
                d.adresa?.toLowerCase().includes(query) ||
                d.telefon?.toLowerCase().includes(query)
            )
            .slice(0, 10);
    }, [dobavljaci, searchQuery]);

    useEffect(() => {
        setSelectedIndex(0);
    }, [filteredDobavljaci.length]);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (searchRef.current && !searchRef.current.contains(event.target as Node)) {
                setShowSearchResults(false);
            }
        };

        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, []);

    const handleSelectDobavljac = (dobavljac: Dobavljac) => {
        setSelectedDobavljac(dobavljac);
        setSearchQuery(dobavljac.naziv);
        setShowSearchResults(false);
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (!showSearchResults || filteredDobavljaci.length === 0) return;

        switch (e.key) {
            case "ArrowDown":
                e.preventDefault();
                setSelectedIndex((prev) => (prev + 1) % filteredDobavljaci.length);
                break;
            case "ArrowUp":
                e.preventDefault();
                setSelectedIndex((prev) => (prev - 1 + filteredDobavljaci.length) % filteredDobavljaci.length);
                break;
            case "Enter":
                e.preventDefault();
                if (filteredDobavljaci[selectedIndex]) {
                    handleSelectDobavljac(filteredDobavljaci[selectedIndex]);
                }
                break;
            case "Escape":
                setShowSearchResults(false);
                break;
        }
    };

    const handleUnosClick = () => {
        if (selectedDobavljac && brojRacuna) {
            navigate('/artikli', {
                state: {
                    dobavljacId: selectedDobavljac.id,
                    dobavljacNaziv: selectedDobavljac.naziv,
                    brojRacuna: brojRacuna
                }
            });
        }
    };

    const canProceed = selectedDobavljac !== null && brojRacuna.trim() !== "";

    return (
        <div className="card">
            <h2 className="text-2xl font-semibold mb-6">Unos robe</h2>

            <div style={{ marginBottom: '2rem' }}>
                <label className="field-label">Broj računa</label>
                <input
                    type="text"
                    placeholder="Unesite broj računa..."
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="input-big"
                    style={{
                        borderColor: brojRacuna ? '#059669' : undefined,
                    }}
                />
            </div>

            <div style={{ marginBottom: '2rem', position: 'relative' }} ref={searchRef}>
                <label className="field-label">
                    Pretraži i izaberi dobavljača
                    <span style={{ fontSize: '0.875rem', fontWeight: 400, marginLeft: '8px', color: '#6b7280' }}>
                        (strelice gore/dole za navigaciju, Enter za izbor)
                    </span>
                </label>
                <input
                    type="text"
                    placeholder="Pretraži dobavljače po nazivu, adresi ili telefonu..."
                    value={searchQuery}
                    onChange={(e) => {
                        setSearchQuery(e.target.value);
                        setShowSearchResults(true);
                        setSelectedDobavljac(null);
                    }}
                    onFocus={() => setShowSearchResults(true)}
                    onKeyDown={handleKeyDown}
                    className="input-big"
                    style={{
                        borderColor: showSearchResults && filteredDobavljaci.length > 0 ? '#2563eb' : undefined,
                        background: selectedDobavljac ? '#f0fdf4' : 'white',
                    }}
                />

                {showSearchResults && searchQuery.trim() && (
                    <div style={{
                        position: 'absolute',
                        top: '100%',
                        left: 0,
                        right: 0,
                        background: 'white',
                        border: '2px solid #e5e7eb',
                        borderRadius: '8px',
                        marginTop: '4px',
                        maxHeight: '400px',
                        overflowY: 'auto',
                        zIndex: 1000,
                        boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)',
                    }}>
                        {filteredDobavljaci.length > 0 ? (
                            filteredDobavljaci.map((dob, idx) => (
                                <div
                                    key={dob.id}
                                    onClick={() => handleSelectDobavljac(dob)}
                                    style={{
                                        padding: '16px',
                                        borderBottom: '1px solid #f3f4f6',
                                        cursor: 'pointer',
                                        transition: 'background 0.15s',
                                        background: idx === selectedIndex ? '#eff6ff' : 'white',
                                    }}
                                    onMouseEnter={() => setSelectedIndex(idx)}
                                >
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                                        <div style={{ flex: 1 }}>
                                            <div style={{ fontWeight: 600, color: '#111827', fontSize: '1.125rem', marginBottom: '4px' }}>
                                                {dob.naziv}
                                            </div>
                                            {dob.adresa && (
                                                <div style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '2px' }}>
                                                    Adresa: {dob.adresa}
                                                </div>
                                            )}
                                            {dob.telefon && (
                                                <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>
                                                    Telefon: {dob.telefon}
                                                </div>
                                            )}
                                            <div style={{ fontSize: '0.75rem', color: '#9ca3af', marginTop: '4px' }}>
                                                ID: {dob.id}
                                            </div>
                                        </div>
                                        <button
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                handleSelectDobavljac(dob);
                                            }}
                                            style={{
                                                background: idx === selectedIndex ? '#2563eb' : '#3b82f6',
                                                color: 'white',
                                                padding: '8px 16px',
                                                borderRadius: '6px',
                                                border: 'none',
                                                cursor: 'pointer',
                                                fontSize: '0.875rem',
                                                fontWeight: 600,
                                                marginLeft: '16px',
                                            }}
                                        >
                                            Izaberi
                                        </button>
                                    </div>
                                </div>
                            ))
                        ) : (
                            <div style={{
                                padding: '24px',
                                textAlign: 'center',
                                color: '#6b7280',
                            }}>
                                Nema rezultata za &quot;{searchQuery}&quot;
                            </div>
                        )}
                    </div>
                )}
            </div>

            {selectedDobavljac && (
                <div style={{
                    background: '#f0fdf4',
                    border: '2px solid #86efac',
                    borderRadius: '12px',
                    padding: '20px',
                    marginBottom: '2rem',
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                        <div>
                            <h3 style={{ fontWeight: 600, color: '#059669', fontSize: '1.25rem', marginBottom: '8px' }}>
                                Izabrani dobavljac
                            </h3>
                            <p style={{ fontSize: '1.125rem', fontWeight: 600, color: '#111827', marginBottom: '4px' }}>
                                {selectedDobavljac.naziv}
                            </p>
                            {selectedDobavljac.adresa && (
                                <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '2px' }}>
                                    Adresa: {selectedDobavljac.adresa}
                                </p>
                            )}
                            {selectedDobavljac.telefon && (
                                <p style={{ fontSize: '0.875rem', color: '#6b7280' }}>
                                    Telefon: {selectedDobavljac.telefon}
                                </p>
                            )}
                        </div>
                        <button
                            onClick={() => {
                                setSelectedDobavljac(null);
                                setSearchQuery("");
                            }}
                            style={{
                                background: '#dc2626',
                                color: 'white',
                                padding: '8px 16px',
                                borderRadius: '6px',
                                border: 'none',
                                cursor: 'pointer',
                                fontSize: '0.875rem',
                                fontWeight: 600,
                            }}
                        >
                            Promeni
                        </button>
                    </div>
                </div>
            )}

            {!selectedDobavljac && !searchQuery && (
                <div style={{ marginTop: '2rem' }}>
                    <h3 className="text-lg font-semibold mb-4">Ili izaberite iz liste svih dobavljača</h3>
                    <div style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
                        gap: '16px',
                        maxHeight: '400px',
                        overflowY: 'auto',
                        padding: '8px',
                    }}>
                        {dobavljaci.map((dob) => (
                            <div
                                key={dob.id}
                                onClick={() => handleSelectDobavljac(dob)}
                                style={{
                                    background: 'white',
                                    border: '2px solid #e5e7eb',
                                    borderRadius: '12px',
                                    padding: '16px',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.borderColor = '#3b82f6';
                                    e.currentTarget.style.boxShadow = '0 4px 6px -1px rgba(0, 0, 0, 0.1)';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.borderColor = '#e5e7eb';
                                    e.currentTarget.style.boxShadow = 'none';
                                }}
                            >
                                <div style={{ fontWeight: 600, color: '#111827', marginBottom: '8px', fontSize: '1rem' }}>
                                    {dob.naziv}
                                </div>
                                {dob.adresa && (
                                    <div style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '4px' }}>
                                        Adresa: {dob.adresa}
                                    </div>
                                )}
                                {dob.telefon && (
                                    <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>
                                        Telefon: {dob.telefon}
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div style={{
                marginTop: '2rem',
                padding: '24px',
                background: canProceed ? '#f0fdf4' : '#f9fafb',
                border: `2px solid ${canProceed ? '#86efac' : '#e5e7eb'}`,
                borderRadius: '12px',
            }}>
                <h3 style={{ fontWeight: 600, fontSize: '1.125rem', marginBottom: '16px', color: '#111827' }}>
                    Pregled unosa
                </h3>
                
                <div style={{ marginBottom: '16px' }}>
                    <div style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                        <span style={{ fontWeight: 600, color: '#6b7280', minWidth: '120px' }}>Broj računa:</span>
                        <span style={{ color: brojRacuna ? '#059669' : '#dc2626', fontWeight: brojRacuna ? 600 : 400 }}>
                            {brojRacuna || '[Nije unet]'}
                        </span>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <span style={{ fontWeight: 600, color: '#6b7280', minWidth: '120px' }}>Dobavljac:</span>
                        <span style={{ color: selectedDobavljac ? '#059669' : '#dc2626', fontWeight: selectedDobavljac ? 600 : 400 }}>
                            {selectedDobavljac ? selectedDobavljac.naziv : '[Nije izabran]'}
                        </span>
                    </div>
                </div>

                <button
                    onClick={handleUnosClick}
                    disabled={!canProceed}
                    className="button-big"
                    style={{
                        background: canProceed ? '#059669' : '#9ca3af',
                        cursor: canProceed ? 'pointer' : 'not-allowed',
                        opacity: canProceed ? 1 : 0.6,
                        maxWidth: '300px',
                        marginTop: '8px',
                    }}
                >
                    {canProceed ? 'Unos artikala' : 'Popunite sva polja'}
                </button>

                {canProceed && (
                    <p style={{ fontSize: '0.875rem', color: '#6b7280', marginTop: '12px' }}>
                        Kliknite na dugme da otvorite formu za unos artikala
                    </p>
                )}
            </div>
        </div>
    );
}
