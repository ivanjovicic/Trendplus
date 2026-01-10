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
        <div 
            className="card"
            style={{
                background: "linear-gradient(to bottom, #ffffff, #fafbfc)",
                boxShadow: "0 10px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)",
                border: "1px solid #e5e7eb"
            }}
        >
            <h2 className="text-2xl font-semibold mb-6" style={{ color: "#1f2937" }}>📦 Unos robe</h2>

            <div 
                style={{ 
                    marginBottom: '2rem',
                    background: "white",
                    padding: "1.5rem",
                    borderRadius: "12px",
                    border: "1px solid #e5e7eb",
                    boxShadow: "0 2px 8px rgba(0,0,0,0.06)"
                }}
            >
                <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>
                    Broj računa <span style={{ color: "#ef4444" }}>*</span>
                </label>
                <input
                    type="text"
                    placeholder="Unesite broj računa..."
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="input-big"
                    style={{
                        borderColor: brojRacuna ? '#10b981' : '#d1d5db',
                        boxShadow: brojRacuna 
                            ? "0 0 0 3px rgba(16,185,129,0.1)" 
                            : "0 1px 2px rgba(0,0,0,0.05)",
                        background: brojRacuna ? "linear-gradient(to right, #ffffff, #ecfdf5)" : "white",
                        transition: "all 0.2s ease"
                    }}
                    onFocus={(e) => e.currentTarget.style.boxShadow = "0 0 0 3px rgba(59,130,246,0.1)"}
                />
            </div>

            <div 
                style={{ 
                    marginBottom: '2rem', 
                    position: 'relative',
                    background: "white",
                    padding: "1.5rem",
                    borderRadius: "12px",
                    border: "1px solid #e5e7eb",
                    boxShadow: "0 2px 8px rgba(0,0,0,0.06)"
                }} 
                ref={searchRef}
            >
                <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>
                    Pretraži i izaberi dobavljača <span style={{ color: "#ef4444" }}>*</span>
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
                        borderColor: showSearchResults && filteredDobavljaci.length > 0 ? '#3b82f6' : '#d1d5db',
                        background: selectedDobavljac 
                            ? "linear-gradient(to right, #ffffff, #ecfdf5)" 
                            : 'white',
                        boxShadow: showSearchResults 
                            ? "0 0 0 3px rgba(59,130,246,0.1)" 
                            : "0 1px 2px rgba(0,0,0,0.05)",
                        transition: "all 0.2s ease"
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
                        borderRadius: '12px',
                        marginTop: '8px',
                        maxHeight: '400px',
                        overflowY: 'auto',
                        zIndex: 1000,
                        boxShadow: '0 10px 25px -5px rgba(0,0,0,0.2), 0 8px 10px -6px rgba(0,0,0,0.1)',
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
                                        transition: 'all 0.15s ease',
                                        background: idx === selectedIndex 
                                            ? 'linear-gradient(to right, #eff6ff, #dbeafe)' 
                                            : 'white',
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
                                                background: idx === selectedIndex 
                                                    ? 'linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%)' 
                                                    : 'linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)',
                                                color: 'white',
                                                padding: '8px 16px',
                                                borderRadius: '8px',
                                                border: 'none',
                                                cursor: 'pointer',
                                                fontSize: '0.875rem',
                                                fontWeight: 600,
                                                marginLeft: '16px',
                                                boxShadow: '0 4px 6px -1px rgba(59,130,246,0.4)',
                                                transition: "all 0.2s ease"
                                            }}
                                            onMouseEnter={(e) => e.currentTarget.style.transform = "scale(1.05)"}
                                            onMouseLeave={(e) => e.currentTarget.style.transform = "scale(1)"}
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
                    background: 'linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%)',
                    border: '2px solid #34d399',
                    borderRadius: '12px',
                    padding: '24px',
                    marginBottom: '2rem',
                    boxShadow: '0 4px 12px rgba(16,185,129,0.2)',
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                        <div>
                            <h3 style={{ fontWeight: 600, color: '#047857', fontSize: '1.25rem', marginBottom: '8px' }}>
                                ✅ Izabrani dobavljač
                            </h3>
                            <p style={{ fontSize: '1.125rem', fontWeight: 600, color: '#111827', marginBottom: '4px' }}>
                                {selectedDobavljac.naziv}
                            </p>
                            {selectedDobavljac.adresa && (
                                <p style={{ fontSize: '0.875rem', color: '#065f46', marginBottom: '2px' }}>
                                    Adresa: {selectedDobavljac.adresa}
                                </p>
                            )}
                            {selectedDobavljac.telefon && (
                                <p style={{ fontSize: '0.875rem', color: '#065f46' }}>
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
                                background: 'linear-gradient(135deg, #dc2626 0%, #b91c1c 100%)',
                                color: 'white',
                                padding: '8px 16px',
                                borderRadius: '8px',
                                border: 'none',
                                cursor: 'pointer',
                                fontSize: '0.875rem',
                                fontWeight: 600,
                                boxShadow: '0 4px 6px -1px rgba(220,38,38,0.4)',
                                transition: "all 0.2s ease"
                            }}
                            onMouseEnter={(e) => e.currentTarget.style.transform = "scale(1.05)"}
                            onMouseLeave={(e) => e.currentTarget.style.transform = "scale(1)"}
                        >
                            Promeni
                        </button>
                    </div>
                </div>
            )}

            {!selectedDobavljac && !searchQuery && (
                <div 
                    style={{ 
                        marginTop: '2rem',
                        background: "white",
                        padding: "1.5rem",
                        borderRadius: "12px",
                        border: "1px solid #e5e7eb",
                        boxShadow: "0 2px 8px rgba(0,0,0,0.06)"
                    }}
                >
                    <h3 className="text-lg font-semibold mb-4" style={{ color: "#374151" }}>
                        📋 Ili izaberite iz liste svih dobavljača
                    </h3>
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
                                    background: 'linear-gradient(to bottom, #ffffff, #f9fafb)',
                                    border: '2px solid #e5e7eb',
                                    borderRadius: '12px',
                                    padding: '16px',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s ease',
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.borderColor = '#3b82f6';
                                    e.currentTarget.style.boxShadow = '0 8px 16px rgba(59,130,246,0.2)';
                                    e.currentTarget.style.transform = 'translateY(-2px)';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.borderColor = '#e5e7eb';
                                    e.currentTarget.style.boxShadow = 'none';
                                    e.currentTarget.style.transform = 'translateY(0)';
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
                background: canProceed 
                    ? 'linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%)' 
                    : 'linear-gradient(to bottom, #f9fafb, #f3f4f6)',
                border: `2px solid ${canProceed ? '#34d399' : '#d1d5db'}`,
                borderRadius: '12px',
                boxShadow: canProceed 
                    ? '0 4px 12px rgba(16,185,129,0.2)' 
                    : '0 2px 8px rgba(0,0,0,0.06)',
            }}>
                <h3 style={{ 
                    fontWeight: 600, 
                    fontSize: '1.125rem', 
                    marginBottom: '16px', 
                    color: canProceed ? '#047857' : '#374151'
                }}>
                    📊 Pregled unosa
                </h3>
                
                <div style={{ marginBottom: '16px' }}>
                    <div style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                        <span style={{ fontWeight: 600, color: '#6b7280', minWidth: '120px' }}>Broj računa:</span>
                        <span style={{ color: brojRacuna ? '#047857' : '#dc2626', fontWeight: brojRacuna ? 600 : 400 }}>
                            {brojRacuna || '[Nije unet]'}
                        </span>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <span style={{ fontWeight: 600, color: '#6b7280', minWidth: '120px' }}>Dobavljač:</span>
                        <span style={{ color: selectedDobavljac ? '#047857' : '#dc2626', fontWeight: selectedDobavljac ? 600 : 400 }}>
                            {selectedDobavljac ? selectedDobavljac.naziv : '[Nije izabran]'}
                        </span>
                    </div>
                </div>

                <button
                    onClick={handleUnosClick}
                    disabled={!canProceed}
                    className="button-big"
                    style={{
                        background: canProceed 
                            ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)' 
                            : 'linear-gradient(135deg, #9ca3af 0%, #6b7280 100%)',
                        cursor: canProceed ? 'pointer' : 'not-allowed',
                        opacity: canProceed ? 1 : 0.6,
                        maxWidth: '300px',
                        marginTop: '8px',
                        boxShadow: canProceed 
                            ? '0 4px 12px rgba(16,185,129,0.4)' 
                            : 'none',
                        border: "none",
                        transition: "all 0.2s ease"
                    }}
                    onMouseEnter={(e) => canProceed && (e.currentTarget.style.transform = "translateY(-2px)")}
                    onMouseLeave={(e) => e.currentTarget.style.transform = "translateY(0)"}
                >
                    {canProceed ? '✅ Unos artikala' : '⚠️ Popunite sva polja'}
                </button>

                {canProceed && (
                    <p style={{ fontSize: '0.875rem', color: '#065f46', marginTop: '12px', fontWeight: 500 }}>
                        💡 Kliknite na dugme da otvorite formu za unos artikala
                    </p>
                )}
            </div>
        </div>
    );
}
