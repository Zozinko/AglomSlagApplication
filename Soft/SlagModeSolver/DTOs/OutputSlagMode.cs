namespace SlagModeSolver.DTOs;

public class OutputSlagMode
{
        /// <summary>
        /// Основность шлака CaO / SiO2
        /// </summary>
        public double SlagBasicity1 { get; set; }

        /// <summary>
        /// Основность шлака (CaO + MgO) / SiO2
        /// </summary>
        public double SlagBasicity2 { get; set; }

        /// <summary>
        /// Основность шлака (CaO + MgO) / (SiO2 + Al2O3)
        /// </summary>
        public double SlagBasicity3 { get; set; }

        /// <summary>
        /// Основность шлака по Куликову
        /// </summary>
        public double SlagBasicityKulikov { get; set; }

        /// <summary>
        ///  Расчётный выход шлака
        /// </summary>
        public double SlagOut { get; set; }

        /// <summary>
        /// Расход материалов
        /// </summary>
        public double MaterialCons { get; set; }

        /// <summary>
        /// Всего ЖРМ
        /// </summary>
        public double TotalMat {get; set;}

        /// <summary>
        /// Вязкость при 1400
        /// </summary>
        public double Viscosity1400 { get; set; }

        /// <summary>
        /// Вязкость при 1450
        /// </summary>
        public double Viscosity1450 { get; set; }

        /// <summary>
        /// Вязкость при 1500
        /// </summary>
        public double Viscosity1500 { get; set; }

        /// <summary>
        /// Вязкость при 1550
        /// </summary>
        public double Viscosity1550 { get; set; }

        /// <summary>
        /// Температура шлака при 7 пуаз
        /// </summary>
        public double Temp7Puaz { get; set; }

        /// <summary>
        /// Параметры градиента
        /// </summary>
        public double Gradient725 { get; set; }
        public double Gradient14001500 { get; set; }

        /// <summary>
        /// Температура шлака
        /// </summary>
        public double SlagTemperature { get; set; }

        /// <summary>
        /// Температура шлака(при 25 пуаз), °С
        /// </summary>
        public double SlagTemperature25Puaz { get; set; }

        /// <summary>
        /// Вязкость шлака при текущей температуре
        /// </summary>
        public double CurrSlagViscosity { get; set; }

        /// <summary>
        /// Расчетный выход шлака по балансу шлакообразующих
        /// </summary>
        public double BalSlagMass { get; set; }

        /// <summary>
        /// Выход шлака (баланс СаО) 
        ///</summary>
        public double CaOBalSlagMass { get; set; }

        ///<summary>
        /// Масса серы, вносимая в печь, кг/т чугуна
        /// </summary>
        public double TotalSInOre { get; set; }

        ///<summary>
        /// Коэффициент активности серы в чугуне
        /// </summary>
        public double SActivity { get; set; }

        ///<summary>
        /// Коэффициент распределения серы
        /// </summary>
        public double SDistribution { get; set; }

        ///<summary>
        /// Содержание серы в чугуне, %
        /// </summary>
        public double SContentInCastIron { get; set; }

        ///<summary>
        /// Температура чугуна (не понимаю нужно ли его выводить)
        /// </summary>
        public double CastIronTemp { get; set; }

        ///<summary>
        /// Справочник использованных шихтовых материалов
        /// </summary>
        public List<Dictionary<string, double>> MaterialParts { get; set; }
    }