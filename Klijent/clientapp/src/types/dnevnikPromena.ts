export interface DnevnikPromenaItem {
  id: number;
  tipPromene: string;
  datum: string;
  iznos: number;
  brojRacuna?: string | null;
  artikalId?: number | null;
  artikalNaziv?: string | null;
  dobavljacId?: number | null;
  dobavljacNaziv?: string | null;
  staraProdajnaCena?: number | null;
  novaProdajnaCena?: number | null;
  komentar?: string | null;
  korisnikIme?: string | null;
  dataOrigin?: string | null;
}

export interface DnevnikPromenaResponse {
  items: DnevnikPromenaItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDir: string;
}

export interface DnevnikPromenaDetail {
  id: number;
  tipPromene: string;
  datum: string;
  artikalId?: number | null;
  nazivArtikla?: string | null;
  kolicina?: number | null;
  staraCena?: number | null;
  novaCena?: number | null;
  iznos: number;
  brojRacuna?: string | null;
  korisnikIme?: string | null;
  komentar?: string | null;
  dataOrigin?: string | null;
  sourceId: number;
}
