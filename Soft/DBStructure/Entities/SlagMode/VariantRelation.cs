using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DBStructure.Entities.SlagMode
{
    public class VariantRelation
    {
        [Key]
        public int ParentId { get; set; }
        [Key]
        public int ChildId { get; set; }

        public List<CalcVariant> VariantParents { get; set; } = new();
        public List<CalcVariant> VariantChildren { get; set; } = new();

    }
}
