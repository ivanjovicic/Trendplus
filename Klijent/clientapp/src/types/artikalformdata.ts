export interface ArtikalFormData {
   /* plu: string | null;*/
    naziv: string;
    prodajnaCena: number;
    nabavnaCena: number | null;
    nabavnaCenaDin?: number | null;  
    prvaProdajnaCena?: number | null;
    tipObuceId: number | null;
    dobavljacId: number | null;
    komentar?: string | null;
    kolicina?: number | null;
}