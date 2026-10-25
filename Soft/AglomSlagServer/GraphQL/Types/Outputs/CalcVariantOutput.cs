using HotChocolate;

namespace AglomSlagServer.Types.Outputs;

[GraphQLName("CalcVariant")]
public sealed class CalcVariantOutput
{
    public int VariantId { get; set; }
    public int BfId { get; set; }
    public DateTime CalcDate { get; set; }
    public DateTime? SaveTime { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public bool IsImport { get; set; }
    public float CokeConsumption { get; set; }
    public float CokeSContent { get; set; }
    public float CokeAshAmount { get; set; }
    public float CokeAshCaOContent { get; set; }
    public float CokeAshSiO2Content { get; set; }
    public float CokeAshAl2O3Content { get; set; }
    public float CokeAshMgOContent { get; set; }
    public float SlagSContent { get; set; }
    public float SlagCaOContent { get; set; }
    public float SlagSiO2Content { get; set; }
    public float SlagTiO2Content { get; set; }
    public float SlagAl2O3Content { get; set; }
    public float SlagMgOContent { get; set; }
    public float CiTemperature { get; set; }
    public float CiSiContent { get; set; }
    public float CiSContent { get; set; }
    public float CiMnContent { get; set; }
    public float CiCContent { get; set; }
    public float CiTiContent { get; set; }
    public float CiCrContent { get; set; }
    public int? ParentId { get; set; }
    public required BlastFurnaceReference BlastFurnace { get; set; }
    public CalcVariantReference? Parent { get; set; }
    public List<CalcVariantReference> Children { get; set; } = [];
    public List<ComponentVariantOutput> ComponentVariants { get; set; } = [];
}

