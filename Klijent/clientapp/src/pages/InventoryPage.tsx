import React, { useEffect, useState } from "react";
import { getInventoryBalance, getInventoryList } from "../services/analyticsApi";
import type { InventoryBalance, InventoryListItem } from "../types/analytics";

export default function InventoryPage() {
  const [balance, setBalance] = useState<InventoryBalance | null>(null);
  const [items, setItems] = useState<InventoryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    Promise.all([getInventoryBalance(true), getInventoryList(1, 50)])
      .then(([bal, list]) => {
        if (!mounted) return;
        setBalance(bal);
        setItems(list.items ?? []);
      })
      .catch((err) => {
        console.error(err);
        if (mounted) setError(err?.message ?? String(err));
      })
      .finally(() => {
        if (mounted) setLoading(false);
      });

    return () => {
      mounted = false;
    };
  }, []);

  if (loading) return <div>Učitavanje bilansa zaliha...</div>;
  if (error) return <div className="text-red-400">Greška: {error}</div>;

  return (
    <div>
      <h2 className="text-2xl font-semibold">Bilans stanja</h2>

      {balance ? (
        <div className="grid grid-cols-4 gap-4 mt-4">
          <div className="p-3 bg-[#121217] rounded-md">SKU ukupno: {balance.totalSkuCount}</div>
          <div className="p-3 bg-[#121217] rounded-md">Na stanju: {balance.totalOnHand}</div>
          <div className="p-3 bg-[#121217] rounded-md">Niski nivo: {balance.lowStockCount}</div>
          <div className="p-3 bg-[#121217] rounded-md">Nema na stanju: {balance.outOfStockCount}</div>
        </div>
      ) : null}

      <section className="mt-6">
        <h3 className="text-lg font-medium">Prvih 50 artikala</h3>
        <table className="w-full mt-2 text-sm">
          <thead>
            <tr className="text-left text-[#9fa9ba] border-b border-[#24262b]">
              <th className="p-2">Naziv</th>
              <th className="p-2">SKU</th>
              <th className="p-2">Dobavljač</th>
              <th className="p-2">Količina</th>
            </tr>
          </thead>
          <tbody>
            {items.map((it) => (
              <tr key={it.artiklId} className="border-b border-[#17171a]">
                <td className="p-2">{it.naziv}</td>
                <td className="p-2">{it.sku}</td>
                <td className="p-2">{it.dobavljacNaziv}</td>
                <td className="p-2">{it.kolicina}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
