using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Artikli.Commands.CreateArtikal
{
    public class CreateArtikalHandler : IRequestHandler<CreateArtikalCommand, int>
    {
        private readonly ITrendplusDbContext _db;
        private readonly ILogger<CreateArtikalHandler> _logger;

        public CreateArtikalHandler(ITrendplusDbContext db, ILogger<CreateArtikalHandler> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> Handle(CreateArtikalCommand request, CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogInformation("CreateArtikalHandler incoming request: {@Request}", request);

            var connection = _db.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT new_artikal_id FROM unos_artikla(
                    $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12
                )";

            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.PLU ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.Naziv ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.IDTipObuce ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.IDDobavljac ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.NabavnaCena ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.NabavnaCenaDin ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.PrvaProdajnaCena ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.ProdajnaCena ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.Kolicina ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.Komentar ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.IDObjekat ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)request.IDSezona ?? DBNull.Value });

            var result = await command.ExecuteScalarAsync(ct);
            var newId = Convert.ToInt32(result);

            _logger.LogInformation("Artikal created via PostgreSQL function with Id {Id}", newId);

            return newId;
        }
    }
}
