using AglomSlagServer.Types.Outputs;
using DBStructure.DbContexts;
using DBStructure.Entities.SlagMode;
using Microsoft.EntityFrameworkCore;

namespace AglomGraphQL.Api.Services;

internal static class SlagModeOutputReader
{
    public static async Task<List<BlastFurnaceOutput>> Furnaces(SlagModeContext db, int? id = null)
    {
        var query = db.BlastFurnaces.AsNoTracking().AsQueryable();
        if (id.HasValue) query = query.Where(x => x.BfId == id.Value);
        var rows = await query.Include(x => x.CalcVariants).AsSplitQuery().ToListAsync();
        return rows.Select(row =>
        {
            var dto = SlagModeOutputMapper.Furnace(row);
            dto.CalcVariants = row.CalcVariants.Select(SlagModeOutputMapper.VariantReference).ToList();
            return dto;
        }).ToList();
    }

    public static async Task<List<ComponentGuideOutput>> Guides(SlagModeContext db, int? id = null)
    {
        var query = db.ComponentsGuide.AsNoTracking().AsQueryable();
        if (id.HasValue) query = query.Where(x => x.ComponentId == id.Value);
        var rows = await query.Include(x => x.ComponentVariants).ThenInclude(x => x.CalcVariant)
            .AsSplitQuery().ToListAsync();
        return rows.Select(row =>
        {
            var dto = SlagModeOutputMapper.Guide(row);
            dto.ComponentVariants = row.ComponentVariants.Select(component =>
                Component(component, component.CalcVariant, row)).ToList();
            return dto;
        }).ToList();
    }

    public static async Task<List<CalcVariantOutput>> Variants(SlagModeContext db, int? id = null)
    {
        var query = db.CalcVariants.AsNoTracking().AsQueryable();
        if (id.HasValue) query = query.Where(x => x.VariantId == id.Value);
        // Fixed-depth split queries avoid recursive loading and cartesian expansion.
        var rows = await query.Include(x => x.BlastFurnace).Include(x => x.Parent)
            .Include(x => x.Children).Include(x => x.ComponentVariants).ThenInclude(x => x.ComponentGuide)
            .AsSplitQuery().ToListAsync();
        return rows.Select(row =>
        {
            var dto = new CalcVariantOutput
            {
                BlastFurnace = SlagModeOutputMapper.FurnaceReference(row.BlastFurnace),
                Parent = row.Parent is null ? null : SlagModeOutputMapper.VariantReference(row.Parent),
                Children = row.Children.Select(SlagModeOutputMapper.VariantReference).ToList(),
                ComponentVariants = row.ComponentVariants.Select(component =>
                    Component(component, row, component.ComponentGuide)).ToList()
            };
            SlagModeOutputMapper.Variant(row, dto);
            return dto;
        }).ToList();
    }

    private static ComponentVariantOutput Component(ComponentVariant row, CalcVariant variant, ComponentGuide guide)
    {
        var dto = new ComponentVariantOutput
        {
            CalcVariant = SlagModeOutputMapper.VariantReference(variant),
            ComponentGuide = SlagModeOutputMapper.GuideReference(guide)
        };
        SlagModeOutputMapper.Component(row, dto);
        return dto;
    }
}
