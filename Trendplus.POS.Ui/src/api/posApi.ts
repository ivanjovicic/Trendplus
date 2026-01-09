// Use environment variable for API base URL, fallback to empty string (use Vite proxy)
const BASE_URL = import.meta.env.VITE_API_BASE_URL || "";

export type SaleItem = {
    productId: number;
    qty: number;
};

export type PosProduct = {
    id: number;
    name: string;
    price: number;
    kolicina?: number;
};

export type SaleListItem = {
    id: number;
    brojRacuna: string;
    datumProdaje: string;
    ukupanIznos: number;
    brojStavki: number;
    nacinPlacanja: string;
};

export type SalesListResponse = {
    items: SaleListItem[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
};

// Type for raw artikal from backend
interface RawArtikal {
    id: number;
    naziv: string;
    prodajnaCena: number;
    kolicina?: number;
}

// Fetch products from Trendplus2 backend API
export async function getProducts(): Promise<PosProduct[]> {
    const res = await fetch(`${BASE_URL}/artikli`);
    if (!res.ok) {
        const text = await res.text();
        throw new Error(`Failed to load products: ${res.status} ${text}`);
    }
    const artikli: RawArtikal[] = await res.json();
    
    // Map Artikal to PosProduct
    return artikli.map((a) => ({
        id: a.id,
        name: a.naziv,
        price: a.prodajnaCena,
        kolicina: a.kolicina
    }));
}

export async function createSale(items: SaleItem[]) {
    // Post sale to Trendplus2 backend
    const res = await fetch(`${BASE_URL}/api/prodaja`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
        body: JSON.stringify({
            brojRacuna: `POS-${Date.now()}`,
            idObjekat: 1, // Terminal ID
            nacinPlacanja: "Gotovina",
            stavke: items.map(item => ({
                idArtikal: item.productId,
                kolicina: item.qty,
                cena: 0 // Backend will fetch from DB
            }))
        })
    });

    if (!res.ok) {
        const text = await res.text();
        throw new Error(`Failed to create sale: ${res.status} ${text}`);
    }

    return res.json();
}

export async function getSalesHistory(
    pageNumber: number = 1,
    pageSize: number = 50,
    fromDate?: string,
    toDate?: string
): Promise<SalesListResponse> {
    const params = new URLSearchParams({
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
    });

    if (fromDate) params.append("fromDate", fromDate);
    if (toDate) params.append("toDate", toDate);

    const res = await fetch(`${BASE_URL}/api/prodaje?${params.toString()}`);
    if (!res.ok) {
        const text = await res.text();
        throw new Error(`Failed to load sales: ${res.status} ${text}`);
    }

    return res.json();
}