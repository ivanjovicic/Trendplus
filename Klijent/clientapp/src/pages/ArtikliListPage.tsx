import React from "react";
import { Link } from "react-router-dom";
import { getArtikli } from "../services/artikliApi";

type ArtikalListItem = {
  id: number;
  naziv: string;
  prodajnaCena: number;
};

export default function ArtikliListPage() {
  const [artikli, setArtikli] = React.useState<ArtikalListItem[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let aborted = false;

    const load = async () => {
      try {
        const lista = await getArtikli();
        if (!aborted) {
          setArtikli(lista ?? []);
          setLoading(false);
        }
      } catch (e: any) {
        if (!aborted) {
          console.error(e);
          setError(e?.message ?? "Greška pri učitavanju artikala.");
          setLoading(false);
        }
      }
    };

    load();

    return () => {
      aborted = true;
    };
  }, []);

  if (loading) {
    return (
      <div className="card">
        <p>Učitavanje artikala...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card">
        <p className="error-msg">{error}</p>
      </div>
    );
  }

  return (
    <div className="card" style={{ margin: "2rem auto", maxWidth: 900 }}>
      <h2 className="text-2xl font-semibold mb-4">Lista artikala</h2>
      <table className="w-full">
        <thead>
          <tr>
            <th style={{ textAlign: "left" }}>Naziv</th>
            <th style={{ textAlign: "right" }}>Prodajna cena</th>
            <th style={{ textAlign: "center" }}>Akcije</th>
          </tr>
        </thead>
        <tbody>
          {artikli.map((a) => (
            <tr key={a.id}>
              <td>{a.naziv}</td>
              <td style={{ textAlign: "right" }}>{a.prodajnaCena}</td>
              <td style={{ textAlign: "center" }}>
                <Link to={`/artikli/${a.id}/edit`} className="btn-link">
                  Izmeni
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}