namespace AglomSlagServer.Types.Inputs.SlagMode;

public class CalcVariantInput
{
    public int BfId { get; set; }
    // Compatibility field only: ownership is always assigned by the server.
    [System.ComponentModel.DefaultValue(0)]
    public int UserId { get; set; }
    public DateTime CalcDate { get; set; }
    public required string VariantName { get; set; }
    public bool IsImport { get; set; }
    
    // Coke
    public float CokeConsumption { get; set; }
    public float CokeSContent { get; set; }
    public float CokeAshAmount { get; set; }
    public float CokeAshCaOContent { get; set; }
    public float CokeAshSiO2Content { get; set; }
    public float CokeAshAl2O3Content { get; set; }
    public float CokeAshMgOContent { get; set; }
    
    // Slag
    [System.ComponentModel.DefaultValue(0f)]
    public float SlagSContent { get; set; }
    public float SlagCaOContent { get; set; }
    public float SlagSiO2Content { get; set; }
    public float SlagTiO2Content { get; set; }
    public float SlagAl2O3Content { get; set; }
    public float SlagMgOContent { get; set; }
    
    // Castiron
    public float CiTemperature { get; set; }
    public float CiSiContent { get; set; }
    public float CiSContent { get; set; }
    public float CiMnContent { get; set; }
    public float CiCContent { get; set; }
    public float CiTiContent { get; set; }
    public float CiCrContent { get; set; }
}
