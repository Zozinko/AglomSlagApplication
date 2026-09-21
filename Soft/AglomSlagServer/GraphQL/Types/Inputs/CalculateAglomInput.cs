namespace AglomSlagServer.Types.Inputs;

public class CalculateAglomInput
{
    public int UserId { get; set; }
    public CompositionOfCokeAshInput CompositionOfCokeAsh { get; set; }
    public CompositionOfCokeInput CompositionOfCoke { get; set; }
    public FluxAdditionsInput FluxAdditions { get; set; }
    public List<ChargeComponentInput> ChargeComponents { get; set; }
    public AglomParametersInput AglomParameters { get; set; }
    public bool CreatePreset { get; set; }
}