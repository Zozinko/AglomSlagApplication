using AglomSlagServer.Types;
using AglomSlagServer.Types.Inputs;
using AglomGraphQL.Api.Services;
using AglomSlagServer.Types.Inputs.SlagMode;
using SlagModeSolver.DTOs;
using DBStructure.Entities.SlagMode;

namespace AglomSlagServer.Mutations;

public class Mutation(SlagModeCalculationService slagCalcService, AglomCalculationService aglomCalcService, SlagModeQueryService slagModeService)
{
    public AglomCalculationResult CalculateAglom(CalculateAglomInput input)
    {
        return aglomCalcService.Calculate(input);
    }

    public OutputSlagMode CalculateSlagMode(InputDataModel input)
    {
        return slagCalcService.Calculate(input);
    }

    // ========== BLAST FURNACE MUTATIONS ==========
    
    public async Task<BlastFurnace> AddBlastFurnace(string bfName) =>
        await slagModeService.AddBlastFurnaceAsync(bfName);

    public async Task<BlastFurnace?> UpdateBlastFurnace(int bfId, string bfName) =>
        await slagModeService.UpdateBlastFurnaceAsync(bfId, bfName);

    public async Task<bool> DeleteBlastFurnace(int bfId) =>
        await slagModeService.DeleteBlastFurnaceAsync(bfId);

    // ========== COMPONENT GUIDE MUTATIONS ==========
    
    public async Task<ComponentGuide> AddComponentGuide(ComponentGuideInput input) =>
        await slagModeService.AddComponentGuideAsync(input);

    public async Task<ComponentGuide?> UpdateComponentGuide(int componentId, ComponentGuideInput input) =>
        await slagModeService.UpdateComponentGuideAsync(componentId, input);

    public async Task<bool> DeleteComponentGuide(int componentId) =>
        await slagModeService.DeleteComponentGuideAsync(componentId);

    // ========== CALC VARIANT MUTATIONS ==========
    
    public async Task<CalcVariant> AddCalcVariant(CalcVariantInput input, List<ComponentVariantInput> components) =>
        await slagModeService.AddCalcVariantAsync(input, components);

    public async Task<CalcVariant?> UpdateCalcVariant(int variantId, CalcVariantInput input, List<ComponentVariantInput> components) =>
        await slagModeService.UpdateCalcVariantAsync(variantId, input, components);

    public async Task<bool> DeleteCalcVariant(int variantId) =>
        await slagModeService.DeleteCalcVariantAsync(variantId);
}