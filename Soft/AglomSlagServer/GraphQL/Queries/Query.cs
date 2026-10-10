using AglomGraphQL.Api.Services;
using DBStructure.Entities.SlagMode;

namespace AglomSlagServer.Queries;

public class Query(SlagModeQueryService slagModeService)
{

    public string Hello() => "Aglom GraphQL API";
    
    public async Task<List<BlastFurnace>> GetBlastFurnaces() => 
        await slagModeService.GetBlastFurnacesAsync();

    public async Task<List<ComponentGuide>> GetComponentGuides() => 
        await slagModeService.GetComponentGuidesAsync();

    public async Task<List<CalcVariant>> GetCalcVariants() => 
        await slagModeService.GetCalcVariantsAsync();

    public async Task<CalcVariant?> GetCalcVariantById(int variantId) => 
        await slagModeService.GetCalcVariantByIdAsync(variantId);
}