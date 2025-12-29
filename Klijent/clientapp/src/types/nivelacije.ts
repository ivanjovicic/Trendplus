export interface NivelacijaItem {
  id: number;
  datum: string;
  artikalId: number | null;
  artikalNaziv?: string | null;
  staraProdajnaCena: number | null;
  novaProdajnaCena: number | null;
  komentar: string | null;
  korisnikIme: string | null;
}

export interface NivelacijeResponse {
  items: NivelacijaItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDir: string;
}
