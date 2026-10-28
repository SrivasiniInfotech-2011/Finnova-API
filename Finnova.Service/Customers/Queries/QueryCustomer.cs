using MediatR;
using Finnova.Models.Contracts.Customers;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.Queries;

public sealed record QueryCustomer(CustomerQueryRequest Request) : IRequest<IReadOnlyCollection<CustomerResponse>>;

public sealed class QueryCustomerHandler(
    ICustomerReadRepository read,
    ICustomerProjectionMapper mapper) : IRequestHandler<QueryCustomer, IReadOnlyCollection<CustomerResponse>>
{
    public async Task<IReadOnlyCollection<CustomerResponse>> Handle(QueryCustomer q, CancellationToken ct)
    {
        var result = await read.SearchAsync(q.Request, ct);
        var projected = new List<CustomerResponse>(result.Items.Count);
        foreach (var customer in result.Items)
            projected.Add(await mapper.ProjectAsync(customer, ct));
        return projected;
    }
}
