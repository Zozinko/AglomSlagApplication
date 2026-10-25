using HotChocolate;

namespace AglomSlagServer.Types.Outputs;

[GraphQLName("BlastFurnace")]
public sealed class BlastFurnaceOutput
{
    public int BfId { get; set; }
    public string BfName { get; set; } = string.Empty;
    public List<CalcVariantReference> CalcVariants { get; set; } = [];
}

