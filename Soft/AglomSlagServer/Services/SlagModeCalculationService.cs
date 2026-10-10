using SlagModeSolver.DTOs;
using SlagCalculator = SlagModeSolver.SlagModeSolver;

namespace AglomGraphQL.Api.Services;

public class SlagModeCalculationService
{
    public OutputSlagMode Calculate(InputDataModel inputData)
    {
        try
        {
            // 1. Защита от null-ссылок, которые убивают DLL
            if (inputData == null)
                throw new ArgumentNullException(nameof(inputData), "Входные данные не могут быть null");
            
            if (inputData.Components == null || !inputData.Components.Any())
                throw new ArgumentException("Список компонентов (Components) не может быть пустым или null");

            var totalMaterials = inputData.Components.Sum(c => c.Consumption);
            if (totalMaterials <= 0)
                throw new ArgumentException("Суммарный расход материалов (Consumption) должен быть больше 0");

            // 2. Вызов расчёта
            return SlagModeSolver.SlagModeSolver.GetTableData(inputData);
        }
        catch (Exception ex)
        {
            // 3. Жёсткое логирование в консоль сервера
            Console.WriteLine("========================================");
            Console.WriteLine("!!! ОШИБКА В SlagModeSolver !!!");
            Console.WriteLine($"Тип ошибки: {ex.GetType().Name}");
            Console.WriteLine($"Сообщение: {ex.Message}");
            Console.WriteLine($"Стек: {ex.StackTrace}");
            Console.WriteLine("========================================");

            // Пробрасываем ошибку дальше, чтобы GraphQL её показал (благодаря Шагу 1)
            throw new InvalidOperationException($"Ошибка расчёта: {ex.Message}", ex);
        }
    }
}