namespace AglomSlagServer.Types.Outputs;

// Scalar reference deliberately terminates navigation traversal.
public sealed class ComponentGuideReference
{
    public int ComponentId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public float FeContent { get; set; }
    public float SiO2Content { get; set; }
    public float Al2O3Content { get; set; }
    public float CaOContent { get; set; }
    public float MgOContent { get; set; }
    public float SContent { get; set; }
    public float MnOContent { get; set; }
    public float TiO2Content { get; set; }
}

