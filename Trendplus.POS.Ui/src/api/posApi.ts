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

// Fetch products from Trendplus2 backend API
export async function getProducts(): Promise<PosProduct[]> {
    const res = await fetch(`${BASE_URL}/artikli`);
    if (!res.ok) {
        const text = await res.text();
        throw new Error(`Failed to load products: ${res.status} ${text}`);
    }
    const artikli = await res.json();
    
    // Map Artikal to PosProduct
    return artikli.map((a: any) => ({
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