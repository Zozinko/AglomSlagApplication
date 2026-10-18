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

    public async Task<List<BlastFurnace>> GetBlastFurnacesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BlastFurnaces.ToListAsync();
    }

    public async Task<List<ComponentGuide>> GetComponentGuidesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ComponentsGuide.ToListAsync();
    }

    public async Task<List<CalcVariant>> GetCalcVariantsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.CalcVariants
            .Include(cv => cv.BlastFurnace)
            .Include(cv => cv.ComponentVariants)
            .ToListAsync();
    }

    public async Task<CalcVariant?> GetCalcVariantByIdAsync(int variantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.CalcVariants
            .Include(cv => cv.BlastFurnace)
            .Include(cv => cv.ComponentVariants)
            .FirstOrDefaultAsync(cv => cv.VariantId == variantId);
    }

    // ========== BLAST FURNACE CRUD ==========
    
    public async Task<BlastFurnace> AddBlastFurnaceAsync(string bfName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bf = new BlastFurnace { BfName = bfName };
        context.BlastFurnaces.Add(bf);
        await context.SaveChangesAsync();
        return bf;
    }

    public async Task<BlastFurnace?> UpdateBlastFurnaceAsync(int bfId, string bfName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bf = await context.BlastFurnaces.FindAsync(bfId);
        if (bf == null) return null;
        
        bf.BfName = bfName;
        await context.SaveChangesAsync();
        return bf;
    }

    public async Task<bool> DeleteBlastFurnaceAsync(int bfId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bf = await context.BlastFurnaces
            .Include(b => b.CalcVariants)
            .FirstOrDefaultAsync(b => b.BfId == bfId);
        
        if (bf == null) return false;

        if (bf.CalcVariants.Any())
        {
            var variantList = string.Join(", ", 
                bf.CalcVariants.Select(cv => $"{cv.VariantName} (от {cv.SaveTime:dd.MM.yyyy HH:mm})"));
            throw new InvalidOperationException(
                $"Невозможно удалить печь '{bf.BfName}', так как она используется в вариантах расчёта: {variantList}");
        }

        context.BlastFurnaces.Remove(bf);
        await context.SaveChangesAsync();
        return true;
    }

    // ========== COMPONENT GUIDE CRUD ==========
    
    public async Task<ComponentGuide> AddComponentGuideAsync(ComponentGuideInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
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
        return component;
    }

    public async Task<ComponentGuide?> UpdateComponentGuideAsync(int componentId, ComponentGuideInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var component = await context.ComponentsGuide.FindAsync(componentId);
        if (component == null) return null;

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
        return component;
    }

    public async Task<bool> DeleteComponentGuideAsync(int componentId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var component = await context.ComponentsGuide
            .Include(c => c.ComponentVariants)
            .ThenInclude(cv => cv.CalcVariant)
            .FirstOrDefaultAsync(c => c.ComponentId == componentId);

        if (component == null) return false;

        if (component.ComponentVariants.Any())
        {
            var variantList = string.Join(", ",
                component.ComponentVariants
                    .Select(cv => cv.CalcVariant)
                    .Where(cv => cv != null)
                    .Select(cv => $"{cv!.VariantName} (от {cv.SaveTime:dd.MM.yyyy HH:mm})")
                    .Distinct());
            throw new InvalidOperationException(
                $"Невозможно удалить компонент '{component.ComponentName}', так как он используется в вариантах расчёта: {variantList}");
        }

        context.ComponentsGuide.Remove(component);
        await context.SaveChangesAsync();
        return true;
    }

    // ========== CALC VARIANT CRUD ==========
    
    public async Task<CalcVariant> AddCalcVariantAsync(CalcVariantInput input, List<ComponentVariantInput> components)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bf = await context.BlastFurnaces.FindAsync(input.BfId) 
            ?? throw new InvalidOperationException($"Печь с ID {input.BfId} не найдена");

        var variant = new CalcVariant
        {
            BfId = input.BfId,
            BlastFurnace = bf,
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
        return variant;
    }

    public async Task<CalcVariant?> UpdateCalcVariantAsync(int variantId, CalcVariantInput input, List<ComponentVariantInput> components)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var variant = await context.CalcVariants
            .Include(v => v.ComponentVariants)
            .FirstOrDefaultAsync(v => v.VariantId == variantId);

        if (variant == null) return null;

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
        return variant;
    }

    public async Task<bool> DeleteCalcVariantAsync(int variantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var variant = await context.CalcVariants.FindAsync(variantId);
        if (variant == null) return false;

        context.CalcVariants.Remove(variant);
        await context.SaveChangesAsync();
        return true;
    }
}