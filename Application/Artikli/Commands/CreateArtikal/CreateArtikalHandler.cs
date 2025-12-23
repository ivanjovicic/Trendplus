using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

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
                throw new ArgumentNullException(nameof(request));

            var newIdParam = new SqlParameter("@NewArtikalId", System.Data.SqlDbType.Int)
            {
                Direction = System.Data.ParameterDirection.Output
            };

            var parameters = new[]
            {
    new SqlParameter("@PLU", request.PLU),
    new SqlParameter("@Naziv", request.Naziv),
    new SqlParameter("@IDTipObuce", (object?)request.IDTipObuce ?? DBNull.Value),
    new SqlParameter("@IDDobavljac", (object?)request.IDDobavljac ?? DBNull.Value),
    new SqlParameter("@NabavnaCena", (object?)request.NabavnaCena ?? DBNull.Value),
    new SqlParameter("@NabavnaCenaDin", (object?)request.NabavnaCenaDin ?? DBNull.Value),
    new SqlParameter("@PrvaProdajnaCena", (object?)request.PrvaProdajnaCena ?? DBNull.Value),
    new SqlParameter("@ProdajnaCena", (object?)request.ProdajnaCena ?? DBNull.Value),
    new SqlParameter("@Kolicina", request.Kolicina),
    new SqlParameter("@Komentar", (object?)request.Komentar ?? DBNull.Value),
    new SqlParameter("@IDObjekat", (object?)request.IDObjekat ?? DBNull.Value),
    new SqlParameter("@IDSezona", (object?)request.IDSezona ?? DBNull.Value),
    newIdParam
};

            var newId = await _db.ExecuteStoredProcedureWithOutputAsync(
                @"EXEC dbo.UnosArtikla 
        @PLU,
        @Naziv,
        @IDTipObuce,
        @IDDobavljac,
        @NabavnaCena,
        @NabavnaCenaDin,
        @PrvaProdajnaCena,
        @ProdajnaCena,
        @Kolicina,
        @Komentar,
        @IDObjekat,
        @IDSezona,
        @NewArtikalId OUTPUT",
                parameters,
                ct
            );

            _logger.LogInformation("Artikal created via stored procedure with Id {Id}", newId);

            return newId;

        }
    }
}
