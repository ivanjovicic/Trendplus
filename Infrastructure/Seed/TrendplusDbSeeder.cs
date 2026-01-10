using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seed;

public static class TrendplusDbSeeder
{
    public static async Task SeedAsync(TrendplusDbContext db, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var year = DateTime.UtcNow.Year;

        async Task<TipObuce> GetOrCreateTipAsync(string naziv)
        {
            var existing = await db.TipoviObuce.FirstOrDefaultAsync(x => x.Naziv == naziv, ct);
            if (existing != null) return existing;

            var created = new TipObuce { Naziv = naziv };
            db.TipoviObuce.Add(created);
            await db.SaveChangesAsync(ct);
            return created;
        }

        async Task<Dobavljac> GetOrCreateDobavljacAsync(Dobavljac seed)
        {
            var existing = await db.Dobavljaci.FirstOrDefaultAsync(x => x.Naziv == seed.Naziv, ct);
            if (existing != null) return existing;

            db.Dobavljaci.Add(seed);
            await db.SaveChangesAsync(ct);
            return seed;
        }

        async Task<Sezona> GetOrCreateSezonaAsync(Sezona seed)
        {
            var existing = await db.Sezone.FirstOrDefaultAsync(x => x.Naziv == seed.Naziv, ct);
            if (existing != null) return existing;

            db.Sezone.Add(seed);
            await db.SaveChangesAsync(ct);
            return seed;
        }

        // Tipovi obuce
        var tipPatike = await GetOrCreateTipAsync("Patike");
        var tipCizme = await GetOrCreateTipAsync("Cizme");
        var tipSandale = await GetOrCreateTipAsync("Sandale");
        var tipPapuce = await GetOrCreateTipAsync("Papuce");

        // Dobavljaci
        var dobNike = await GetOrCreateDobavljacAsync(new Dobavljac
        {
            Naziv = "Nike Srbija d.o.o.",
            Adresa = "Bulevar Mihajla Pupina 10, Beograd",
            Telefon = "+381 11 123 456",
            Napomena = "Test dobavljac (seed)"
        });

        var dobAdidas = await GetOrCreateDobavljacAsync(new Dobavljac
        {
            Naziv = "Adidas Partner d.o.o.",
            Adresa = "Narodnog fronta 21, Novi Sad",
            Telefon = "+381 21 987 654",
            Napomena = "Test dobavljac (seed)"
        });

        var dobLocal = await GetOrCreateDobavljacAsync(new Dobavljac
        {
            Naziv = "Lokalni Distributer",
            Adresa = "Industrijska zona bb, Nis",
            Telefon = "+381 18 555 111",
            Napomena = "Test dobavljac (seed)"
        });

        // Sezone
        var sezonaLeto = await GetOrCreateSezonaAsync(new Sezona
        {
            Naziv = $"Prolece/Leto {year}",
            DatumOd = new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DatumDo = new DateTime(year, 8, 31, 23, 59, 59, DateTimeKind.Utc)
        });

        var sezonaZima = await GetOrCreateSezonaAsync(new Sezona
        {
            Naziv = $"Jesen/Zima {year}/{year + 1}",
            DatumOd = new DateTime(year, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            DatumDo = new DateTime(year + 1, 2, 28, 23, 59, 59, DateTimeKind.Utc)
        });

        // Artikli (dodaj samo ako ne postoji po Naziv)
        async Task<Artikli> GetOrCreateArtikalAsync(Artikli seed)
        {
            var existing = await db.Artikli.FirstOrDefaultAsync(a => a.Naziv == seed.Naziv, ct);
            if (existing != null) return existing;

            seed.UpdatedAt = seed.UpdatedAt == default ? now : seed.UpdatedAt;
            db.Artikli.Add(seed);
            await db.SaveChangesAsync(ct);
            return seed;
        }

        var a1 = await GetOrCreateArtikalAsync(new Artikli
        {
            Naziv = "Nike Air Max 90",
            IDTipObuce = tipPatike.Id,
            IDDobavljac = dobNike.Id,
            IDSezona = sezonaLeto.Id,
            NabavnaCena = 8500m,
            ProdajnaCena = 12990m,
            Kolicina = 15,
            Komentar = "Seed artikal - popularan model",
            IDObjekat = 1,
            UpdatedAt = now
        });

        var a2 = await GetOrCreateArtikalAsync(new Artikli
        {
            Naziv = "Birkenstock Arizona",
            IDTipObuce = tipSandale.Id,
            IDDobavljac = dobLocal.Id,
            IDSezona = sezonaLeto.Id,
            NabavnaCena = 4200m,
            ProdajnaCena = 6990m,
            Kolicina = 20,
            Komentar = "Seed artikal",
            IDObjekat = 1,
            UpdatedAt = now
        });

        await GetOrCreateArtikalAsync(new Artikli
        {
            Naziv = "Adidas Ultraboost",
            IDTipObuce = tipPatike.Id,
            IDDobavljac = dobAdidas.Id,
            IDSezona = sezonaLeto.Id,
            NabavnaCena = 9200m,
            ProdajnaCena = 14990m,
            Kolicina = 9,
            Komentar = "Seed artikal",
            IDObjekat = 1,
            UpdatedAt = now
        });

        await GetOrCreateArtikalAsync(new Artikli
        {
            Naziv = "Timberland Winter Boot",
            IDTipObuce = tipCizme.Id,
            IDDobavljac = dobLocal.Id,
            IDSezona = sezonaZima.Id,
            NabavnaCena = 11000m,
            ProdajnaCena = 17990m,
            Kolicina = 6,
            Komentar = "Seed artikal - zimska kolekcija",
            IDObjekat = 1,
            UpdatedAt = now
        });

        await GetOrCreateArtikalAsync(new Artikli
        {
            Naziv = "Papuce EVA Basic",
            IDTipObuce = tipPapuce.Id,
            IDDobavljac = dobLocal.Id,
            IDSezona = sezonaLeto.Id,
            NabavnaCena = 300m,
            ProdajnaCena = 690m,
            Kolicina = 50,
            Komentar = "Seed artikal - jeftina roba",
            IDObjekat = 1,
            UpdatedAt = now
        });

        // --- DODATNI TEST ARTIKLI (100+) ---
        // Batch insert: u?itaj postoje?e nazive, generiši nove, ubaci samo one koji fale.
        var existingNames = await db.Artikli.AsNoTracking().Select(a => a.Naziv).ToListAsync(ct);
        var existingSet = new HashSet<string>(existingNames, StringComparer.Ordinal);

        var brands = new[] { "Nike", "Adidas", "Puma", "Reebok", "New Balance" };
        var models = new[] { "Runner", "Street", "Classic", "Sport", "Lite" };
        var colors = new[] { "Black", "White", "Blue", "Red", "Gray" };
        var sizes = new[] { 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 };

        var rng = new Random(20250110);
        var generated = new List<Artikli>(capacity: 140);

        for (var i = 0; i < 140; i++)
        {
            var brand = brands[i % brands.Length];
            var model = models[(i / brands.Length) % models.Length];
            var color = colors[(i / (brands.Length * models.Length)) % colors.Length];
            var size = sizes[i % sizes.Length];

            var naziv = $"{brand} {model} {color} {size}";
            if (existingSet.Contains(naziv))
                continue;

            var tipId = (i % 4) switch
            {
                0 => tipPatike.Id,
                1 => tipCizme.Id,
                2 => tipSandale.Id,
                _ => tipPapuce.Id
            };

            var dobId = (i % 3) switch
            {
                0 => dobNike.Id,
                1 => dobAdidas.Id,
                _ => dobLocal.Id
            };

            var sezonaId = (i % 2 == 0) ? sezonaLeto.Id : sezonaZima.Id;

            // price bands per type
            var baseNabavna = (i % 4) switch
            {
                0 => 6500m,
                1 => 9000m,
                2 => 2500m,
                _ => 400m
            };

            var nabavna = baseNabavna + rng.Next(0, 2000);
            var prodajna = Math.Round(nabavna * (decimal)(1.45 + (rng.NextDouble() * 0.35)), 2);
            var kolicina = rng.Next(0, 40);

            generated.Add(new Artikli
            {
                Naziv = naziv,
                IDTipObuce = tipId,
                IDDobavljac = dobId,
                IDSezona = sezonaId,
                NabavnaCena = nabavna,
                ProdajnaCena = prodajna,
                Kolicina = kolicina,
                Komentar = "Seed artikal (generated)",
                IDObjekat = 1,
                UpdatedAt = now
            });

            existingSet.Add(naziv);
        }

        if (generated.Count > 0)
        {
            db.Artikli.AddRange(generated);
            await db.SaveChangesAsync(ct);
        }

        // Test prodaja (seed only once)
        var seedRacun = $"TEST-{DateTime.UtcNow:yyyyMMdd}-001";
        var prodajaExists = await db.ProdajaZaglavlja.AnyAsync(p => p.BrojRacuna == seedRacun, ct);
        if (!prodajaExists)
        {
            var prodaja = new ProdajaZaglavlje
            {
                BrojRacuna = seedRacun,
                DatumProdaje = DateTime.UtcNow,
                NacinPlacanja = "Gotovina",
                IDObjekat = 1,
                Stavke = new List<ProdajaStavka>
                {
                    new() { IdArtikal = a1.Id, Kolicina = 1, Cena = a1.ProdajnaCena ?? 0m },
                    new() { IdArtikal = a2.Id, Kolicina = 2, Cena = a2.ProdajnaCena ?? 0m }
                }
            };

            db.ProdajaZaglavlja.Add(prodaja);
            await db.SaveChangesAsync(ct);
        }
    }
}
