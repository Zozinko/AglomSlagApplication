namespace AglomSlagServer.Types.Outputs;

// Scalar reference deliberately terminates navigation traversal.
public sealed class BlastFurnaceReference
{
    public int BfId { get; set; }
    public string BfName { get; set; } = string.Empty;
}

