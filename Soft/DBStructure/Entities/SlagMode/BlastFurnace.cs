using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DBStructure.Entities.SlagMode
{
    public class BlastFurnace
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BfId{ get; set; }
        public required string BfName { get; set; }

        public List<CalcVariant> CalcVariants { get; set; } = new();
    }
}
