using AglomSlagServer.Auth;
using AglomSlagServer.Types.Outputs;
using DBStructure.DbContexts;
using DBStructure.Entities.SlagMode;
using Microsoft.EntityFrameworkCore;

namespace AglomGraphQL.Api.Services;

internal static class SlagModeOutputReader
{
    private static IQueryable<CalcVariant> VisibleVariants(SlagModeContext db, CurrentActor actor) =>
        db.CalcVariants.AsNoTracking().Where(x => actor.Admin || x.UserId == actor.UserId);

    private static IQueryable<ComponentGuide> VisibleGuides(SlagModeContext db, CurrentActor actor) =>
        db.ComponentsGuide.AsNoTracking().Where(x => actor.Admin || x.UserId == actor.UserId);

    public static async Task<List<BlastFurnaceOutput>> Furnaces(SlagModeContext db, CurrentActor actor, int? id = null)
    {
        var query = db.BlastFurnaces.AsNoTracking().AsQueryable();
        if (id.HasValue) query = query.Where(x => x.BfId == id.Value);
        var rows = await query.ToListAsync();
        var ids = rows.Select(x => x.BfId).ToArray();
        var variants = await VisibleVariants(db, actor).Where(x => ids.Contains(x.BfId)).ToListAsync();
        var visibleParents = await VisibleParentIds(db, actor, variants);
        var byFurnace = variants.ToLookup(x => x.BfId);
        return rows.Select(row =>
        {
            var dto = SlagModeOutputMapper.Furnace(row);
            dto.CalcVariants = byFurnace[row.BfId].Select(x => Reference(x, visibleParents)).ToList();
            return dto;
        }).ToList();
    }

    public static async Task<List<ComponentGuideOutput>> Guides(SlagModeContext db, CurrentActor actor, int? id = null)
    {
        var query = VisibleGuides(db, actor);
        if (id.HasValue) query = query.Where(x => x.ComponentId == id.Value);
        var rows = await query.ToListAsync();
        var ids = rows.Select(x => x.ComponentId).ToArray();
        var components = await db.ComponentVariants.AsNoTracking()
            .Where(x => ids.Contains(x.ComponentId) && (actor.Admin || x.CalcVariant.UserId == actor.UserId))
            .Include(x => x.CalcVariant).ToListAsync();
        var visibleParents = await VisibleParentIds(db, actor, components.Select(x => x.CalcVariant));
        var byGuide = components.ToLookup(x => x.ComponentId);
        return rows.Select(row =>
        {
            var dto = SlagModeOutputMapper.Guide(row);
            dto.ComponentVariants = byGuide[row.ComponentId].Select(component =>
                Component(component, component.CalcVariant, row, visibleParents)).ToList();
            return dto;
        }).ToList();
    }

    public static async Task<List<CalcVariantOutput>> Variants(SlagModeContext db, CurrentActor actor, int? id = null)
    {
        var query = VisibleVariants(db, actor);
        if (id.HasValue) query = query.Where(x => x.VariantId == id.Value);
        // Every related private entity is filtered in SQL before materialization.
        // Separate queries preserve snapshots whose guide or parent is no longer visible.
        var rows = await query.Include(x => x.BlastFurnace).Include(x => x.ComponentVariants)
            .AsSplitQuery().ToListAsync();
        var ids = rows.Select(x => x.VariantId).ToArray();
        var parentIds = rows.Where(x => x.ParentId.HasValue).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var relations = await VisibleVariants(db, actor)
            .Where(x => parentIds.Contains(x.VariantId) || (x.ParentId.HasValue && ids.Contains(x.ParentId.Value)))
            .ToListAsync();
        var parents = relations.ToDictionary(x => x.VariantId);
        var children = relations.Where(x => x.ParentId.HasValue).ToLookup(x => x.ParentId!.Value);
        var visibleParents = await VisibleParentIds(db, actor, rows.Concat(relations));
        var guideIds = rows.SelectMany(x => x.ComponentVariants).Select(x => x.ComponentId).Distinct().ToArray();
        var guides = await VisibleGuides(db, actor).Where(x => guideIds.Contains(x.ComponentId))
            .ToDictionaryAsync(x => x.ComponentId);
        return rows.Select(row =>
        {
            var parent = row.ParentId.HasValue ? parents.GetValueOrDefault(row.ParentId.Value) : null;
            var dto = new CalcVariantOutput
            {
                BlastFurnace = SlagModeOutputMapper.FurnaceReference(row.BlastFurnace),
                Parent = parent is null ? null : Reference(parent, visibleParents),
                Children = children[row.VariantId].Select(x => Reference(x, visibleParents)).ToList(),
                ComponentVariants = row.ComponentVariants.Select(component =>
                    Component(component, row, guides.GetValueOrDefault(component.ComponentId), visibleParents)).ToList()
            };
            SlagModeOutputMapper.Variant(row, dto);
            dto.ParentId = parent?.VariantId;
            return dto;
        }).ToList();
    }

    private static async Task<HashSet<int>> VisibleParentIds(SlagModeContext db, CurrentActor actor, IEnumerable<CalcVariant> rows)
    {
        var ids = rows.Where(x => x.ParentId.HasValue).Select(x => x.ParentId!.Value).Distinct().ToArray();
        return (await VisibleVariants(db, actor).Where(x => ids.Contains(x.VariantId))
            .Select(x => x.VariantId).ToListAsync()).ToHashSet();
    }

    private static CalcVariantReference Reference(CalcVariant row, HashSet<int> visibleParents)
    {
        var dto = SlagModeOutputMapper.VariantReference(row);
        if (dto.ParentId.HasValue && !visibleParents.Contains(dto.ParentId.Value)) dto.ParentId = null;
        return dto;
    }

    private static ComponentVariantOutput Component(ComponentVariant row, CalcVariant variant, ComponentGuide? guide,
        HashSet<int> visibleParents)
    {
        var dto = new ComponentVariantOutput
        {
            CalcVariant = Reference(variant, visibleParents),
            ComponentGuide = guide is null ? null : SlagModeOutputMapper.GuideReference(guide)
        };
        SlagModeOutputMapper.Component(row, dto);
        return dto;
    }
}
