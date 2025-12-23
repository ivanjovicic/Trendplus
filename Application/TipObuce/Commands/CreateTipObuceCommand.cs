using Application.Artikli.Common.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.TipObuce.Commands
{
    public record CreateTipObuceCommand(string Naziv) : IRequest<int>;

    public class CreateTipObuceCommandHandler : IRequestHandler<CreateTipObuceCommand, int>
    {
        private readonly ITrendplusDbContext _context;

        public CreateTipObuceCommandHandler(ITrendplusDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateTipObuceCommand request, CancellationToken cancellationToken)
        {
            var entity = new Domain.Model.TipObuce { Naziv = request.Naziv };

            _context.TipoviObuce.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }

}
