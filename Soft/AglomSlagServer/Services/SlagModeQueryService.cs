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
}