using AglomSlagServer.Types;
using AglomSlagServer.Types.Inputs;
using AglomGraphQL.Api.Services;
using SlagModeSolver.DTOs;

namespace AglomSlagServer.Mutations;

public class Mutation(SlagModeCalculationService slagCalcService, AglomCalculationService aglomCalcService)
{

    public AglomCalculationResult CalculateAglom(CalculateAglomInput input)
    {
        return aglomCalcService.Calculate(input);
    }
    
    public OutputSlagMode CalculateSlagMode(InputDataModel input)
    {
        return slagCalcService.Calculate(input);
    }
}