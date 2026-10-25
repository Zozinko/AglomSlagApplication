using System.Collections;
using AglomSlagServer.Errors;

namespace AglomGraphQL.Api.Services;

internal static class CalculationValidation
{
    public static void Require(bool valid, string message)
    {
        if (!valid) throw new ApiException(PublicErrorCodes.ValidationFailed, message);
    }

    // These DTOs contain trees of public properties, lists and numeric values only.
    // Inspect the complete result, independently of the client's selection set.
    public static void FiniteTree(object? value, bool output = false)
    {
        if (value is null)
        {
            if (output) throw Failed();
            throw new ApiException(PublicErrorCodes.ValidationFailed, "Обязательные данные расчёта отсутствуют.");
        }
        if (value is double number)
        {
            if (!double.IsFinite(number))
            {
                if (output) throw Failed();
                Require(false, "Все числовые данные расчёта должны быть конечными.");
            }
            return;
        }
        if (value is string || value.GetType().IsValueType) return;
        if (value is IDictionary dictionary)
        {
            foreach (var item in dictionary.Values) FiniteTree(item, output);
            return;
        }
        if (value is IEnumerable items)
        {
            foreach (var item in items) FiniteTree(item, output);
            return;
        }
        foreach (var property in value.GetType().GetProperties())
            FiniteTree(property.GetValue(value), output);
    }

    public static void Percentages(object value, params string[] excluded)
    {
        foreach (var property in value.GetType().GetProperties())
        {
            if (property.PropertyType != typeof(double) || excluded.Contains(property.Name)) continue;
            if (property.GetValue(value) is not double number) continue;
            Require(number >= 0 && number <= 100,
                $"Содержание {property.Name} должно быть в пределах 0–100 процентов.");
        }
    }

    public static ApiException Failed() => new(PublicErrorCodes.CalculationFailed,
        "Не удалось получить конечный результат расчёта для заданных данных.");
}
