using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DBStructure.Entities.SlagMode
{
    public class CalcVariant
    {
        #region Other
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VariantId { get; set; }
        public int BFId { get; set; }
        public DateTime CalcDate { get; set; }
        public DateTime SaveTime { get; } = DateTime.Now;
        public required string VariantName { get; set; }
        #endregion

        #region Coke
        public float CokeConsumption { get; set; }
        public float CokeSContent { get; set; }
        public float CokeAshAmount { get; set; }
        public float CokeAshCaOContent { get; set; }
        public float CokeAshSiO2Content { get; set; }
        public float CokeAshAl2O3Content { get; set; }
        public float CokeAshMgOContent { get; set; }

        #endregion

        #region Slag
        public float SlagCaOContent { get; set; }
        public float SlagSiO2Content { get; set; }
        public float SlagTiO2Content { get; set; }

        #endregion
        #region Castiron
        public float CiTemperature { get; set; }
        public float CiSiContent { get; set; }
        public float CiSContent { get; set; }
        public float CiMnContent { get; set; }
        public float CiCContent { get; set; }
        public float CiTiContent { get; set; }
        public float CICrContent { get; set; }
        #endregion

        #region Relations
        public required BlastFurnace BlastFurnace { get; set; }
        public VariantRelation? ParentRelation { get; set; }
        public List<VariantRelation>? ChildRelation { get; set; }
        public required List<ComponentVariant> ComponentVariants { get; set; }
        #endregion
    }
}
