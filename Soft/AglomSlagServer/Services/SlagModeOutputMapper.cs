using AglomSlagServer.Types.Outputs;
using DBStructure.Entities.SlagMode;
using Riok.Mapperly.Abstractions;

namespace AglomGraphQL.Api.Services;

// Mapperly generates all scalar mappings. Relations are explicitly assembled only
// from the bounded queries below; an EF fixup collection never defines API data.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class SlagModeOutputMapper
{
    public static partial BlastFurnaceReference FurnaceReference(BlastFurnace source);
    public static partial ComponentGuideReference GuideReference(ComponentGuide source);
    public static partial CalcVariantReference VariantReference(CalcVariant source);

    [MapperIgnoreTarget(nameof(BlastFurnaceOutput.CalcVariants))]
    public static partial BlastFurnaceOutput Furnace(BlastFurnace source);

    [MapperIgnoreTarget(nameof(ComponentGuideOutput.ComponentVariants))]
    public static partial ComponentGuideOutput Guide(ComponentGuide source);

    [MapperIgnoreTarget(nameof(CalcVariantOutput.BlastFurnace))]
    [MapperIgnoreTarget(nameof(CalcVariantOutput.Parent))]
    [MapperIgnoreTarget(nameof(CalcVariantOutput.Children))]
    [MapperIgnoreTarget(nameof(CalcVariantOutput.ComponentVariants))]
    public static partial void Variant(CalcVariant source, CalcVariantOutput target);

    [MapperIgnoreTarget(nameof(ComponentVariantOutput.ComponentGuide))]
    [MapperIgnoreTarget(nameof(ComponentVariantOutput.CalcVariant))]
    public static partial void Component(ComponentVariant source, ComponentVariantOutput target);
}
