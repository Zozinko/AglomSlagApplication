namespace SlagModeSolver.DTOs;

public class InputDataSlag
{
    /// <summary>Фактическое содержание серы в шлаке, %. Ноль сохраняет прежнее поведение API.</summary>
    [System.ComponentModel.DefaultValue(0d)]
    public double S { get; set; }

    public double CaO { get; set; }

    public double SiO2 { get; set; }

    public double TiO2 { get; set; }

    public double Al2O3 { get; set; }

    public double MgO { get; set; }
}
