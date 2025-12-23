using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repository
{
    using Application.Common.Interfaces;
    using Application.Prodaja.Commands.ProdajArtikle;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Configuration;
    using System.Data;

    public class ProdajaRepository : IProdajaRepository
    {
        private readonly string _connStr;

        public ProdajaRepository(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default");
        }

        public async Task<int> ProdajAsync(
            ProdajArtikleCommand command,
            CancellationToken ct)
        {
            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand("sp_ProdajArtikle", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@BrojRacuna", command.BrojRacuna);
            cmd.Parameters.AddWithValue("@IDObjekat", command.IdObjekat);
            cmd.Parameters.AddWithValue("@NacinPlacanja", command.NacinPlacanja);

            // TVP
            var table = new DataTable();
            table.Columns.Add("IDArtikal", typeof(int));
            table.Columns.Add("Kolicina", typeof(int));
            table.Columns.Add("Cena", typeof(decimal));

            foreach (var s in command.Stavke)
                table.Rows.Add(s.IdArtikal, s.Kolicina, s.Cena);

            var tvp = cmd.Parameters.AddWithValue("@Stavke", table);
            tvp.SqlDbType = SqlDbType.Structured;
            tvp.TypeName = "ProdajaStavkeType";

            await conn.OpenAsync(ct);

            // Ako želiš ID prodaje → SELECT SCOPE_IDENTITY() u SP
            await cmd.ExecuteNonQueryAsync(ct);

            return 0;
        }
    }

}
