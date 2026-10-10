using SlagModeSolver.DTOs;
using SlagSolverLibrary;
using UBFBaseLibrary;
using UBFBaseLibrary.Models;
using UBFCalculationLibrary;

namespace SlagModeSolver;

public class SlagModeSolver
{
    public static OutputSlagMode GetTableData(InputDataModel inputData)
    {
        #region SlagModeData
        
        var totalMaterials = inputData.Components.Sum(component => component.Consumption);
        var calcModel = new SlagMode();
        
        calcModel.BaseChugun = new UBF_Chugun
        {
            Si = inputData.Iron.Si,
            S = inputData.Iron.S,
            Mn = inputData.Iron.Mn,
            C = inputData.Iron.C,
            Ti = inputData.Iron.Ti,
            Cr = inputData.Iron.Cr,
            Temp = inputData.Iron.Temp
        };

        calcModel.BaseSlag = new UBF_Slag()
        {
            CaO = inputData.Slag.CaO,
            SiO2 = inputData.Slag.SiO2,
            TiO2 = inputData.Slag.TiO2
        };

        calcModel.BaseKoks = new UBF_KoksComponent()
        {
            Rashod = inputData.Coke.Consumption,
            Sera = inputData.Coke.Sulfur,
            Zola = inputData.Coke.AshAmount,
            ZolaCaO = inputData.Coke.AshCaOFraction,
            ZolaSiO2 = inputData.Coke.AshSiO2Fraction,
            ZolaAl2O3 = inputData.Coke.AshAl2O3Fraction,
            ZolaMgO = inputData.Coke.AshMgOFraction
        };

        foreach (var component in inputData.Components)
        {
            calcModel.BaseShihta.Add(new UBF_ShihtaComponent
            {
                Name = component.Sourcename,
                Rashod = component.Consumption,
                Fe = component.Fe,
                SiO2 = component.SiO2,
                Al2O3 = component.Al2O3,
                CaO = component.CaO,
                MgO = component.MgO,
                S = component.S,
                MnO = component.MnO,
                TiO2 = component.TiO2,
            });
        }
        
        calcModel.SetPartOfZhRM(calcModel.BaseShihta);

        var data = new OutputSlagMode
        {
            SlagBasicity1 = calcModel.BaseOsnovnost1,
            SlagOut = calcModel.MassOfOxSlagSum,
            MaterialCons = calcModel.RashodRudMat,
            TotalMat = totalMaterials
        };
        data.MaterialParts = [];
        foreach (var component in inputData.Components)
        {
            var componentPart = component.Consumption/totalMaterials;
            data.MaterialParts.Add(new Dictionary<string, double>{ { component.Sourcename, componentPart } });
        }

        #endregion
        
        #region SlagSolverData
        
        var solver = new SlagSolver();
        var solverData = solver.Solve(inputData.Slag.CaO, inputData.Slag.SiO2, inputData.Slag.Al2O3, inputData.Slag.MgO);
        
        data.CaOBalSlagMass = calcModel.UdWeight;
        data.BalSlagMass = calcModel.MassOfOxSlagSum;
        data.SlagBasicity2 = solverData.Osnovnost2;
        data.SlagBasicity3 = solverData.Osnovnost3;
        data.SlagBasicityKulikov = solverData.OsnovnostKulikov;
        data.SlagTemperature = UBF_Functions.GetSlagTemparature(calcModel.BaseChugun);
        data.SlagTemperature25Puaz = solverData.Temp_25_puaz;
        data.CurrSlagViscosity = UBF_Functions.GetSlagViscosityByTemp(solverData.K_b, solverData.K_a, data.SlagTemperature);
        data.Viscosity1400 = solverData.Viscosity_1400;
        data.Viscosity1450 = solverData.Viscosity_1450;
        data.Viscosity1500 = solverData.Viscosity_1500;
        data.Viscosity1550 = solverData.Viscosity_1550;
        data.TotalSInOre = calcModel.GetTotalRuda_S;
        data.SActivity = calcModel.GetActivity_S;
        data.SDistribution = calcModel.LS_Fact;
        data.SContentInCastIron = calcModel.BaseChugun.S;
        data.Temp7Puaz = solverData.Temp_7_puaz;
        data.Gradient725 = solverData.Gradient_7_25;
        data.Gradient14001500 = solverData.Gradient_1400_1500;
        
        #endregion
        return data;
    }
}