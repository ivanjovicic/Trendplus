using MediatR;
using Domain.Model;

namespace Application.Dobavljaci.Queries
{
    public class GetDobavljacQuery : IRequest<List<Dobavljac>>
    {
    }
}
