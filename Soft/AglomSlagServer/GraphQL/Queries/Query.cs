using AglomGraphQL.Api.Services;
using AglomSlagServer.Types.Outputs;

namespace AglomSlagServer.Queries;

public class Query(SlagModeQueryService slagModeService)
{
    public string Hello() => "Aglom GraphQL API";

    public async Task<List<BlastFurnaceOutput>> GetBlastFurnaces() =>
        await slagModeService.GetBlastFurnacesAsync();

    public async Task<List<ComponentGuideOutput>> GetComponentGuides() =>
        await slagModeService.GetComponentGuidesAsync();

    public async Task<List<CalcVariantOutput>> GetCalcVariants() =>
        await slagModeService.GetCalcVariantsAsync();

    public async Task<CalcVariantOutput?> GetCalcVariantById(int variantId) =>
        await slagModeService.GetCalcVariantByIdAsync(variantId);
}
