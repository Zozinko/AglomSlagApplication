using AglomSlagServer.Types;
using AglomSlagServer.Types.Inputs;
using AglomGraphQL.Api.Services;

namespace AglomSlagServer.Mutations;

public class Mutation
{
    private readonly AglomCalculationService _service;

    public Mutation(AglomCalculationService service)
    {
        _service = service;
    }

    public AglomCalculationResult CalculateAglom(CalculateAglomInput input)
    {
        return _service.Calculate(input);
    }
}