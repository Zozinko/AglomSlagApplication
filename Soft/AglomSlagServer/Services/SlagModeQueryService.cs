using AglomSlagServer.Types.Inputs.SlagMode;
using DBStructure.DbContexts;
using DBStructure.Entities.SlagMode;
using Microsoft.EntityFrameworkCore;

namespace AglomGraphQL.Api.Services;

public class SlagModeQueryService
{
    private readonly SlagModeContext _context;

    public SlagModeQueryService(SlagModeContext context)
    {
        _context = context;
    }

    // 1. Получить все доменные печи
    public async Task<List<BlastFurnace>> GetBlastFurnacesAsync()
    {
        return await _context.BlastFurnaces.ToListAsync();
    }

    // 2. Получить справочник всех компонентов
    public async Task<List<ComponentGuide>> GetComponentGuidesAsync()
    {
        return await _context.ComponentsGuide.ToListAsync();
    }

    // 3. Получить все варианты расчётов (с загруженными связями для GraphQL)
    public async Task<List<CalcVariant>> GetCalcVariantsAsync()
    {
        return await _context.CalcVariants
            .Include(cv => cv.BlastFurnace)
            .Include(cv => cv.ComponentVariants)
            .ToListAsync();
    }

    // 4. Получить конкретный вариант расчёта по ID
    public async Task<CalcVariant?> GetCalcVariantByIdAsync(int variantId)
    {
        return await _context.CalcVariants
            .Include(cv => cv.BlastFurnace)
            .Include(cv => cv.ComponentVariants)
            .FirstOrDefaultAsync(cv => cv.VariantId == variantId);
    }

    // ========== BLAST FURNACE CRUD ==========
    
    public async Task<BlastFurnace> AddBlastFurnaceAsync(string bfName)
    {
        var bf = new BlastFurnace { BfName = bfName };
        _context.BlastFurnaces.Add(bf);
        await _context.SaveChangesAsync();
        return bf;
    }

    public async Task<BlastFurnace?> UpdateBlastFurnaceAsync(int bfId, string bfName)
    {
        var bf = await _context.BlastFurnaces.FindAsync(bfId);
        if (bf == null) return null;
        
        bf.BfName = bfName;
        await _context.SaveChangesAsync();
        return bf;
    }

    public async Task<bool> DeleteBlastFurnaceAsync(int bfId)
    {
        var bf = await _context.BlastFurnaces
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

        _context.BlastFurnaces.Remove(bf);
        await _context.SaveChangesAsync();
        return true;
    }

    // ========== COMPONENT GUIDE CRUD ==========
    
    public async Task<ComponentGuide> AddComponentGuideAsync(ComponentGuideInput input)
    {
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

        _context.ComponentsGuide.Add(component);
        await _context.SaveChangesAsync();
        return component;
    }

    public async Task<ComponentGuide?> UpdateComponentGuideAsync(int componentId, ComponentGuideInput input)
    {
        var component = await _context.ComponentsGuide.FindAsync(componentId);
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

        await _context.SaveChangesAsync();
        return component;
    }

    public async Task<bool> DeleteComponentGuideAsync(int componentId)
    {
        var component = await _context.ComponentsGuide
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

        _context.ComponentsGuide.Remove(component);
        await _context.SaveChangesAsync();
        return true;
    }

    // ========== CALC VARIANT CRUD ==========
    
    public async Task<CalcVariant> AddCalcVariantAsync(CalcVariantInput input, List<ComponentVariantInput> components)
    {
        var variant = new CalcVariant
        {
            BfId = input.BfId,
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
            BlastFurnace = await _context.BlastFurnaces.FindAsync(input.BfId) 
                ?? throw new InvalidOperationException($"Печь с ID {input.BfId} не найдена"),
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

        _context.CalcVariants.Add(variant);
        await _context.SaveChangesAsync();
        return variant;
    }

    public async Task<CalcVariant?> UpdateCalcVariantAsync(int variantId, CalcVariantInput input, List<ComponentVariantInput> components)
    {
        var variant = await _context.CalcVariants
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

        _context.ComponentVariants.RemoveRange(variant.ComponentVariants);

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

        await _context.SaveChangesAsync();
        return variant;
    }

    public async Task<bool> DeleteCalcVariantAsync(int variantId)
    {
        var variant = await _context.CalcVariants.FindAsync(variantId);
        if (variant == null) return false;

        _context.CalcVariants.Remove(variant);
        await _context.SaveChangesAsync();
        return true;
    }
}