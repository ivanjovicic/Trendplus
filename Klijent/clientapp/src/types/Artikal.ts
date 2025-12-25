export type Artikal = {
  id: number;
  naziv: string;
  prodajnaCena: number;
  nabavnaCena: number | null;
  nabavnaCenaDin: number | null;
  prvaProdajnaCena: number | null;
  kolicina: number | null;
  komentar: string | null;
  tipObuceId: number | null;
  dobavljacId: number | null;   
};