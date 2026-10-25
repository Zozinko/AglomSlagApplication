using AglomSlagServer.Errors;
using SlagModeSolver.DTOs;
using SlagCalculator = SlagModeSolver.SlagModeSolver;

namespace AglomGraphQL.Api.Services;

public class SlagModeCalculationService
{
    public OutputSlagMode Calculate(InputDataModel inputData)
    {
        CalculationValidation.FiniteTree(inputData);
        CalculationValidation.Require(inputData.Components.Count > 0, "Список материалов не может быть пустым.");
        CalculationValidation.Percentages(inputData.Coke, nameof(InputDataCoke.Consumption));
        CalculationValidation.Require(inputData.Coke.Consumption >= 0, "Расход кокса не может быть отрицательным.");
        CalculationValidation.Percentages(inputData.Iron, nameof(InputDataIron.Temp));
        CalculationValidation.Require(inputData.Iron.Temp > -273.15,
            "Температура чугуна должна быть выше абсолютного нуля (−273,15 °C).");
        CalculationValidation.Percentages(inputData.Slag);
        foreach (var material in inputData.Components)
        {
            CalculationValidation.Require(!string.IsNullOrWhiteSpace(material.Sourcename), "Укажите название материала.");
            CalculationValidation.Require(material.Consumption >= 0, "Расход отдельного материала не может быть отрицательным.");
            CalculationValidation.Percentages(material, nameof(Material.Consumption));
        }
        var total = inputData.Components.Sum(material => material.Consumption);
        CalculationValidation.Require(double.IsFinite(total) && total > 0, "Суммарный расход материалов должен быть конечным и положительным.");
        // The public output contract defines basicities 1 and 2 with SiO2 as divisor.
        CalculationValidation.Require(inputData.Slag.SiO2 > 0, "SiO2 шлака должен быть больше нуля для расчёта основности.");
        try
        {
            var result = SlagCalculator.GetTableData(inputData);
            CalculationValidation.FiniteTree(result, output: true);
            return result;
        }
        catch (ApiException) { throw; }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{PublicErrorCodes.CalculationFailed}: {exception.GetType().Name}");
            throw CalculationValidation.Failed();
        }
    }
}
