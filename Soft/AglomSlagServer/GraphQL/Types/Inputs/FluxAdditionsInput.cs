namespace AglomSlagServer.Types.Inputs;

public class FluxAdditionsInput
{
    public FluxCompositionInput Limestone { get; set; } = new();
    public FluxCompositionInput Dolomite { get; set; } = new();
}