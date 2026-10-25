using AglomSlagServer.Errors;
using AglomSlagServer.Types.Outputs;
using AglomSlagServer.Types.Inputs.SlagMode;
using DBStructure.DbContexts;
using DBStructure.Entities.SlagMode;
using Microsoft.EntityFrameworkCore;

namespace AglomGraphQL.Api.Services;

public class SlagModeQueryService
{
    private readonly IDbContextFactory<SlagModeContext> _contextFactory;

    public SlagModeQueryService(IDbContextFactory<SlagModeContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<BlastFurnaceOutput>> GetBlastFurnacesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await SlagModeOutputReader.Furnaces(context);
    }

    public async Task<List<ComponentGuideOutput>> GetComponentGuidesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await SlagModeOutputReader.Guides(context);
    }

    public async Task<List<CalcVariantOutput>> GetCalcVariantsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await SlagModeOutputReader.Variants(context);
    }

    public async Task<CalcVariantOutput?> GetCalcVariantByIdAsync(int variantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return (await SlagModeOutputReader.Variants(context, variantId)).SingleOrDefault();
    }
    // ========== BLAST FURNACE CRUD ==========
    
    public async Task<BlastFurnaceOutput> AddBlastFurnaceAsync(string bfName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        ValidateName(bfName);
        var bf = new BlastFurnace { BfName = bfName };
        context.BlastFurnaces.Add(bf);
        await context.SaveChangesAsync();
        return (await SlagModeOutputReader.Furnaces(context, bf.BfId)).Single();
    }

    public async Task<BlastFurnaceOutput?> UpdateBlastFurnaceAsync(int bfId, string bfName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bf = await context.BlastFurnaces.FindAsync(bfId);
        if (bf == null) return null;
        
        ValidateName(bfName);
        bf.BfName = bfName;
        await context.SaveChangesAsync();
        return (await SlagModeOutputReader.Furnaces(context, bf.BfId)).Single();
    }

    public async Task<bool> DeleteBlastFurnaceAsync(int bfId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bf = await context.BlastFurnaces.FindAsync(bfId);
        
        if (bf == null) return false;

        if (await context.CalcVariants.AnyAsync(v => v.BfId == bfId))
            throw new ApiException(PublicErrorCodes.ReferenceConflict, "Печь используется в вариантах расчёта.");

        context.BlastFurnaces.Remove(bf);
        await context.SaveChangesAsync();
        return true;
    }

    // ========== COMPONENT GUIDE CRUD ==========
    
    public async Task<ComponentGuideOutput> AddComponentGuideAsync(ComponentGuideInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        ValidateName(input.ComponentName);
        var component = new ComponentGuide
        {
            ComponentName = input.ComponentName,
            UserId = input.UserId,
            FeContent = input.FeContent,
            SiO2Content = input.SiO2Content,
            Al2O3Content = input.Al2O3Content,
            CaOContent = input.CaOContent,
            MgOContent = input.MgOContent,
            SContent = input.SContent,
            MnOContent = input.MnOContent,
            TiO2Content = input.TiO2Content
        };

        context.ComponentsGuide.Add(component);
        await context.SaveChangesAsync();
        return (await SlagModeOutputReader.Guides(context, component.ComponentId)).Single();
    }

    public async Task<ComponentGuideOutput?> UpdateComponentGuideAsync(int componentId, ComponentGuideInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var component = await context.ComponentsGuide.FindAsync(componentId);
        if (component == null) return null;

        ValidateName(input.ComponentName);
        component.ComponentName = input.ComponentName;
        component.UserId = input.UserId;
        component.FeContent = input.FeContent;
        component.SiO2Content = input.SiO2Content;
        component.Al2O3Content = input.Al2O3Content;
        component.CaOContent = input.CaOContent;
        component.MgOContent = input.MgOContent;
        component.SContent = input.SContent;
        component.MnOContent = input.MnOContent;
        component.TiO2Content = input.TiO2Content;

        await context.SaveChangesAsync();
        return (await SlagModeOutputReader.Guides(context, component.ComponentId)).Single();
    }

    public async Task<bool> DeleteComponentGuideAsync(int componentId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var component = await context.ComponentsGuide.FindAsync(componentId);

        if (component == null) return false;

        if (await context.ComponentVariants.AnyAsync(v => v.ComponentId == componentId))
            throw new ApiException(PublicErrorCodes.ReferenceConflict, "Компонент используется в вариантах расчёта.");

        context.ComponentsGuide.Remove(component);
        await context.SaveChangesAsync();
        return true;
    }

    // ========== CALC VARIANT CRUD ==========
    
    public async Task<CalcVariantOutput> AddCalcVariantAsync(CalcVariantInput input, List<ComponentVariantInput> components)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await ValidateVariant(context, input, components);
        var bf = await context.BlastFurnaces.FindAsync(input.BfId) 
            ?? throw new ApiException(PublicErrorCodes.ReferenceNotFound, "Печь не найдена.");

        var variant = new CalcVariant
        {
            BfId = input.BfId,
            BlastFurnace = bf,
            SaveTime = DateTime.UtcNow,
            CalcDate = input.CalcDate,
            VariantName = input.VariantName,
            IsImport = input.IsImport,
            CokeConsumption = input.CokeConsumption,
            CokeSContent = input.CokeSContent,
            CokeAshAmount = input.CokeAshAmount,
            CokeAshCaOContent = input.CokeAshCaOContent,
            CokeAshSiO2Content = input.CokeAshSiO2Content,
            CokeAshAl2O3Content = input.CokeAshAl2O3Content,
            CokeAshMgOContent = input.CokeAshMgOContent,
            SlagSContent = input.SlagSContent,
            SlagCaOContent = input.SlagCaOContent,
            SlagSiO2Content = input.SlagSiO2Content,
            SlagTiO2Content = input.SlagTiO2Content,
            SlagAl2O3Content = input.SlagAl2O3Content,
            SlagMgOContent = input.SlagMgOContent,
            CiTemperature = input.CiTemperature,
            CiSiContent = input.CiSiContent,
            CiSContent = input.CiSContent,
            CiMnContent = input.CiMnContent,
            CiCContent = input.CiCContent,
            CiTiContent = input.CiTiContent,
            CiCrContent = input.CiCrContent,
            ComponentVariants = components.Select(c => new ComponentVariant
            {
                ComponentId = c.ComponentId,
                ComponentName = c.ComponentName,
                Consumption = c.Consumption,
                FeContent = c.FeContent,
                SiO2Content = c.SiO2Content,
                Al2O3Content = c.Al2O3Content,
                CaOContent = c.CaOContent,
                MgOContent = c.MgOContent,
                SContent = c.SContent,
                MnOContent = c.MnOContent,
                TiO2Content = c.TiO2Content
            }).ToList()
        };

        context.CalcVariants.Add(variant);
        await context.SaveChangesAsync();
        return (await SlagModeOutputReader.Variants(context, variant.VariantId)).Single();
    }

    public async Task<CalcVariantOutput?> UpdateCalcVariantAsync(int variantId, CalcVariantInput input, List<ComponentVariantInput> components)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var variant = await context.CalcVariants
            .Include(v => v.ComponentVariants)
            .FirstOrDefaultAsync(v => v.VariantId == variantId);

        if (variant == null) return null;

        await ValidateVariant(context, input, components);
        variant.BfId = input.BfId;
        variant.CalcDate = input.CalcDate;
        variant.VariantName = input.VariantName;
        variant.IsImport = input.IsImport;
        variant.CokeConsumption = input.CokeConsumption;
        variant.CokeSContent = input.CokeSContent;
        variant.CokeAshAmount = input.CokeAshAmount;
        variant.CokeAshCaOContent = input.CokeAshCaOContent;
        variant.CokeAshSiO2Content = input.CokeAshSiO2Content;
        variant.CokeAshAl2O3Content = input.CokeAshAl2O3Content;
        variant.CokeAshMgOContent = input.CokeAshMgOContent;
        variant.SlagSContent = input.SlagSContent;
        variant.SlagCaOContent = input.SlagCaOContent;
        variant.SlagSiO2Content = input.SlagSiO2Content;
        variant.SlagTiO2Content = input.SlagTiO2Content;
        variant.SlagAl2O3Content = input.SlagAl2O3Content;
        variant.SlagMgOContent = input.SlagMgOContent;
        variant.CiTemperature = input.CiTemperature;
        variant.CiSiContent = input.CiSiContent;
        variant.CiSContent = input.CiSContent;
        variant.CiMnContent = input.CiMnContent;
        variant.CiCContent = input.CiCContent;
        variant.CiTiContent = input.CiTiContent;
        variant.CiCrContent = input.CiCrContent;

        context.ComponentVariants.RemoveRange(variant.ComponentVariants);

        variant.ComponentVariants = components.Select(c => new ComponentVariant
        {
            VariantId = variantId,
            ComponentId = c.ComponentId,
            ComponentName = c.ComponentName,
            Consumption = c.Consumption,
            FeContent = c.FeContent,
            SiO2Content = c.SiO2Content,
            Al2O3Content = c.Al2O3Content,
            CaOContent = c.CaOContent,
            MgOContent = c.MgOContent,
            SContent = c.SContent,
            MnOContent = c.MnOContent,
            TiO2Content = c.TiO2Content
        }).ToList();

        await context.SaveChangesAsync();
        return (await SlagModeOutputReader.Variants(context, variant.VariantId)).Single();
    }

    public async Task<bool> DeleteCalcVariantAsync(int variantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var variant = await context.CalcVariants.FindAsync(variantId);
        if (variant == null) return false;

        if (await context.CalcVariants.AnyAsync(v => v.ParentId == variantId))
            throw new ApiException(PublicErrorCodes.ReferenceConflict, "У варианта есть дочерние варианты.");

        context.CalcVariants.Remove(variant);
        await context.SaveChangesAsync();
        return true;
    }
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ApiException(PublicErrorCodes.ValidationFailed, "Имя не должно быть пустым.");
    }

    private static async Task ValidateVariant(SlagModeContext context, CalcVariantInput input,
        List<ComponentVariantInput> components)
    {
        ValidateName(input.VariantName);
        if (input.CalcDate.Kind != DateTimeKind.Utc)
            throw new ApiException(PublicErrorCodes.ValidationFailed, "Дата расчёта должна быть задана в UTC.");
        if (components.Select(c => c.ComponentId).Distinct().Count() != components.Count)
            throw new ApiException(PublicErrorCodes.ValidationFailed, "Компоненты варианта должны иметь уникальные ID.");
        foreach (var component in components) ValidateName(component.ComponentName);
        if (!await context.BlastFurnaces.AnyAsync(b => b.BfId == input.BfId))
            throw new ApiException(PublicErrorCodes.ReferenceNotFound, "Печь не найдена.");
        var ids = components.Select(c => c.ComponentId).ToArray();
        var found = await context.ComponentsGuide.CountAsync(c => ids.Contains(c.ComponentId));
        if (found != ids.Length)
            throw new ApiException(PublicErrorCodes.ReferenceNotFound, "Один или несколько компонентов не найдены.");
    }
}
