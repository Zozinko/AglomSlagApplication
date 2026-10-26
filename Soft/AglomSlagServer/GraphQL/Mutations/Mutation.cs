using AglomSlagServer.Types;
using AglomSlagServer.Types.Inputs;
using AglomGraphQL.Api.Services;
using AglomSlagServer.Types.Inputs.SlagMode;
using SlagModeSolver.DTOs;
using AglomSlagServer.Types.Outputs;
using AglomSlagServer.Auth;
using HotChocolate.Authorization;

namespace AglomSlagServer.Mutations;

[Authorize]
public class Mutation(SlagModeCalculationService slagCalcService, AglomCalculationService aglomCalcService, SlagModeQueryService slagModeService)
{
    [AllowAnonymous]
    public Task<LoginPayload> Login(LoginInput input, [Service] AuthService auth) => auth.LoginAsync(input);

    public Task<bool> Logout([Service] RefreshSessionService sessions, [Service] IHttpContextAccessor http) =>
        sessions.LogoutAsync(http.HttpContext!.User);

    public Task<bool> ChangePassword(string currentPassword, string newPassword,
        [Service] PasswordChangeService passwords, [Service] IHttpContextAccessor http) =>
        passwords.ChangeAsync(http.HttpContext!.User, currentPassword, newPassword);

    public AglomCalculationResult CalculateAglom(CalculateAglomInput input)
    {
        return aglomCalcService.Calculate(input);
    }

    public OutputSlagMode CalculateSlagMode(InputDataModel input)
    {
        return slagCalcService.Calculate(input);
    }

    // ========== BLAST FURNACE MUTATIONS ==========
    
    public async Task<BlastFurnaceOutput> AddBlastFurnace(string bfName) =>
        await slagModeService.AddBlastFurnaceAsync(bfName);

    public async Task<BlastFurnaceOutput?> UpdateBlastFurnace(int bfId, string bfName) =>
        await slagModeService.UpdateBlastFurnaceAsync(bfId, bfName);

    public async Task<bool> DeleteBlastFurnace(int bfId) =>
        await slagModeService.DeleteBlastFurnaceAsync(bfId);

    // ========== COMPONENT GUIDE MUTATIONS ==========
    
    public async Task<ComponentGuideOutput> AddComponentGuide(ComponentGuideInput input) =>
        await slagModeService.AddComponentGuideAsync(input);

    public async Task<ComponentGuideOutput?> UpdateComponentGuide(int componentId, ComponentGuideInput input) =>
        await slagModeService.UpdateComponentGuideAsync(componentId, input);

    public async Task<bool> DeleteComponentGuide(int componentId) =>
        await slagModeService.DeleteComponentGuideAsync(componentId);

    // ========== CALC VARIANT MUTATIONS ==========
    
    public async Task<CalcVariantOutput> AddCalcVariant(CalcVariantInput input, List<ComponentVariantInput> components) =>
        await slagModeService.AddCalcVariantAsync(input, components);

    public async Task<CalcVariantOutput?> UpdateCalcVariant(int variantId, CalcVariantInput input, List<ComponentVariantInput> components) =>
        await slagModeService.UpdateCalcVariantAsync(variantId, input, components);

    public async Task<bool> DeleteCalcVariant(int variantId) =>
        await slagModeService.DeleteCalcVariantAsync(variantId);
}
