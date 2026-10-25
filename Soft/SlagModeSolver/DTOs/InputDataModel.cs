namespace SlagModeSolver.DTOs;

public class InputDataModel
{
    public required InputDataCoke Coke { get; set; }

    public required InputDataIron Iron { get; set; }

    public required InputDataSlag Slag { get; set; }

    public required List<Material> Components { get; set; }
}