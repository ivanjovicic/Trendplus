using MediatR;
using Domain.Model;

namespace Application.TipObuce.Queries
{
    public class GetTipObuceQuery : IRequest<List<Domain.Model.TipObuce>>
    {
    }
}
