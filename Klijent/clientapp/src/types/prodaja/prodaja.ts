export interface ProdajaStavkaDto {
    idArtikal: number;
    kolicina: number;
    cena: number;
}

export interface KreirajProdajuDto {
    brojRacuna: string;
    idObjekat: number;
    nacinPlacanja: string;
    stavke: ProdajaStavkaDto[];
}
