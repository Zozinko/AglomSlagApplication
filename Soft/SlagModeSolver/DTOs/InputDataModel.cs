namespace SlagModeSolver.DTOs;

public class InputDataModel
{
    public InputDataCoke Coke { get; set; }

    public InputDataIron Iron { get; set; }

    public InputDataSlag Slag { get; set; }

    public List<Material> Components { get; set; }
}