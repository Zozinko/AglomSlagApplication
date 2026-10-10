using AglomGraphQL.Api.Services;
using DBStructure.Entities.SlagMode;

namespace AglomSlagServer.Queries;

public class Query(SlagModeQueryService slagModeService)
{
    private readonly SlagModeQueryService _slagModeService = slagModeService;

    public string Hello() => "Aglom GraphQL API";
    
    public async Task<List<BlastFurnace>> GetBlastFurnaces() => 
        await _slagModeService.GetBlastFurnacesAsync();

    public async Task<List<ComponentGuide>> GetComponentGuides() => 
        await _slagModeService.GetComponentGuidesAsync();

    public async Task<List<CalcVariant>> GetCalcVariants() => 
        await _slagModeService.GetCalcVariantsAsync();

    public async Task<CalcVariant?> GetCalcVariantById(int variantId) => 
        await _slagModeService.GetCalcVariantByIdAsync(variantId);
}