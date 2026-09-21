namespace AglomSlagServer.Types;

public class AglomCalculationResult
{
    public int Id { get; set; }
    public string Message { get; set; }
    public List<AglomComponentResult> Components { get; set; }
}