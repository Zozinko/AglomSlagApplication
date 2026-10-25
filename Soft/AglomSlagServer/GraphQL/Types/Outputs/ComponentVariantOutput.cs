using HotChocolate;

namespace AglomSlagServer.Types.Outputs;

[GraphQLName("ComponentVariant")]
public sealed class ComponentVariantOutput
{
    public int VariantId { get; set; }
    public int ComponentId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public float Consumption { get; set; }
    public float FeContent { get; set; }
    public float SiO2Content { get; set; }
    public float Al2O3Content { get; set; }
    public float CaOContent { get; set; }
    public float MgOContent { get; set; }
    public float SContent { get; set; }
    public float MnOContent { get; set; }
    public float TiO2Content { get; set; }
    public required ComponentGuideReference ComponentGuide { get; set; }
    public required CalcVariantReference CalcVariant { get; set; }
}

