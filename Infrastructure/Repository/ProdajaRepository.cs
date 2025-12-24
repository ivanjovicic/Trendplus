using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Prodaja.Commands.ProdajArtikle;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Repository
{
    public class ProdajaRepository : IProdajaRepository
    {
        private readonly string _connStr;

        public ProdajaRepository(IConfiguration config)
        {
            // koristimo DefaultConnection (PostgreSQL connection string)
            _connStr = config.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        public async Task<int> ProdajAsync(
            ProdajArtikleCommand command,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            // Serijalizuj stavke u JSON (camelCase da se poklopi sa funkcijom)
            var stavkeJson = JsonSerializer.Serialize(command.Stavke, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sp_prodaj_artikle_json($1::varchar, $2::integer, $3::varchar, $4::jsonb)";

            cmd.Parameters.Add(new NpgsqlParameter { Value = command.BrojRacuna ?? string.Empty, NpgsqlDbType = NpgsqlDbType.Varchar });
            cmd.Parameters.Add(new NpgsqlParameter { Value = command.IdObjekat, NpgsqlDbType = NpgsqlDbType.Integer });
            cmd.Parameters.Add(new NpgsqlParameter { Value = command.NacinPlacanja ?? string.Empty, NpgsqlDbType = NpgsqlDbType.Varchar });
            cmd.Parameters.Add(new NpgsqlParameter { Value = stavkeJson, NpgsqlDbType = NpgsqlDbType.Jsonb });

            var result = await cmd.ExecuteScalarAsync(ct);
            var prodajaId = Convert.ToInt32(result);

            return prodajaId;
        }
    }
}