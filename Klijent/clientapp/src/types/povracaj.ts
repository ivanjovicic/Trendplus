// TypeScript types for povraćaj (Return Note)

export interface PovracajZaglavlje {
  id: number;
  brojZapisnika: string;
  datumPovracaja: string;
  dobavljacId: number;
  dobavljacNaziv?: string;
  razlogPovracaja?: string;
  status: string; // "Kreiran", "Poslat", "Prihvaćen", "Odbijen"
  ukupanIznos: number;
  komentar?: string;
  kreatorKorisnik?: string;
  odobrioKorisnik?: string;
  datumKreiranja: string;
  datumOdobrenja?: string;
  brojStavki?: number;
}

export interface PovracajStavka {
  id?: number;
  idArtikal: number;
  artikalNaziv?: string;
  kolicina: number;
  cena: number;
  iznos?: number;
  razlog?: string;
  stanjeArtikla?: string; // "Oštećeno", "Pogrešna veličina", "Neprodat", "Dobar"
}

export interface PovracajDetaljno extends PovracajZaglavlje {
  dobavljac: {
    id: number;
    naziv: string;
  };
  stavke: Array<PovracajStavka & {
    artikal: {
      id: number;
      naziv: string;
    };
  }>;
}

export interface KreirajPovracajRequest {
  idDobavljac: number;
  razlogPovracaja?: string;
  komentar?: string;
  stavke: Array<{
    idArtikal: number;
    kolicina: number;
    cena: number;
    razlog?: string;
    stanjeArtikla?: string;
  }>;
}

export interface KreirajPovracajResponse {
  success: boolean;
  povracajId: number;
  brojZapisnika: string;
  ukupanIznos: number;
  message: string;
}

export interface PovracajListResponse {
  items: PovracajZaglavlje[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

// Stanja artikla za dropdown
export const STANJA_ARTIKLA = [
  "Oštećeno",
  "Pogrešna veličina",
  "Pogrešan model",
  "Neprodat",
  "Dobar",
  "Ostalo"
] as const;

export type StanjeArtikla = typeof STANJA_ARTIKLA[number];
