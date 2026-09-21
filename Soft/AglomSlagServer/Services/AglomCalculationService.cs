using AglomSlagServer.Types;
using AglomSlagServer.Types.Inputs;
using AglomCalculator.Contracts;
using AglomCalculator.Services;

namespace AglomGraphQL.Api.Services;

public class AglomCalculationService
{
    private readonly AglomCalculatorService _calculator;

    public AglomCalculationService(AglomCalculatorService calculator)
    {
        _calculator = calculator;
    }

    private CalculateAglomRequest MapToCalculatorRequest(CalculateAglomInput input)
    {
        return new CalculateAglomRequest
        {
            ChargeComponents = input.ChargeComponents.Select(x => new ChargeComponentRequest
            {
                Name = x.Name,
                Weight = x.Weight,
                Wet = x.Wet,
                PMPP = x.PMPP,
                Fe = x.Fe,
                FeO = x.FeO,
                CaO = x.CaO,
                SiO2 = x.SiO2,
                Al2O3 = x.Al2O3,
                MgO = x.MgO,
                TiO2 = x.TiO2,
                S = x.S,
                P = x.P,
                Cr = x.Cr,
                Zn = x.Zn,
                MnO = x.MnO
            }).ToList(),

            FluxAdditions = new FluxAdditionsRequest
            {
                Limestone = new FluxCompositionRequest
                {
                    CaO = input.FluxAdditions.Limestone.CaO,
                    SiO2 = input.FluxAdditions.Limestone.SiO2,
                    Al2O3 = input.FluxAdditions.Limestone.Al2O3,
                    MgO = input.FluxAdditions.Limestone.MgO
                },
                Dolomite = new FluxCompositionRequest
                {
                    CaO = input.FluxAdditions.Dolomite.CaO,
                    SiO2 = input.FluxAdditions.Dolomite.SiO2,
                    Al2O3 = input.FluxAdditions.Dolomite.Al2O3,
                    MgO = input.FluxAdditions.Dolomite.MgO
                }
            },

            CompositionOfCoke = new CompositionOfCokeRequest
            {
                Consumption = input.CompositionOfCoke.Consumption,
                PercentAsh = input.CompositionOfCoke.PercentAsh,
                PercentSulfur = input.CompositionOfCoke.PercentSulfur,
                PercentVolatiles = input.CompositionOfCoke.PercentVolatiles
            },

            AglomParameters = new AglomParametersRequest
            {
                Basicity = input.AglomParameters.Basicity,
                FeOInAgl = input.AglomParameters.FeOinAgl,
                FluxType = input.AglomParameters.FluxType,
            },

            CompositionOfCokeAsh = new CompositionOfCokeAshRequest
            {
                Fe = input.CompositionOfCokeAsh.Fe,
                CaO = input.CompositionOfCokeAsh.CaO,
                SiO2 = input.CompositionOfCokeAsh.SiO2,
                Al2O3 = input.CompositionOfCokeAsh.Al2O3,
                MgO = input.CompositionOfCokeAsh.MgO,
                P = input.CompositionOfCokeAsh.P
            }
        };
    }

    public AglomCalculationResult Calculate(CalculateAglomInput input)
    {
        var calculatorRequest = MapToCalculatorRequest(input);
        var calculatorResponse = _calculator.Calculate(calculatorRequest);

        return new AglomCalculationResult
        {
            Id = input.UserId,
            Message = "Расчёт выполнен",
            Components = calculatorResponse.Components
                .Select(x => new AglomComponentResult
                {
                    ComponentName = x.ComponentName,
                    ReportComponentOfCharge = x.ReportComponentOfCharge,
                    ReportFe = x.ReportFe,
                    ReportS = x.ReportS,
                    ReportP = x.ReportP,
                    ReportFeO = x.ReportFeO,
                    ReportFe2O3 = x.ReportFe2O3,
                    ReportCaO = x.ReportCaO,
                    ReportSiO2 = x.ReportSiO2,
                    ReportAl2O3 = x.ReportAl2O3,
                    ReportMgO = x.ReportMgO,
                    ReportMnO = x.ReportMnO,
                    ReportTiO2 = x.ReportTiO2,
                    ReportZn = x.ReportZn,
                    ReportPMPP = x.ReportPMPP,
                    ReportCaO_SiO2 = x.ReportCaO_SiO2,
                    ReportOxideSum = x.ReportOxideSum
                })
                .ToList()
        };
    }
}