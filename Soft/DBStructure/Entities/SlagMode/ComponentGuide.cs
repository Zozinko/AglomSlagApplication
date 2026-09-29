using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DBStructure.Entities.SlagMode
{
    public class ComponentGuide
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ComponentId { get; set; }
        public required string ComponentName { get; set; }
        public int UserId { get; set; }
        public float FeContent { get; set; }
        public float SiO2Content { get; set; }
        public float Al2O3Content { get; set; }
        public float CaOContent { get; set; }
        public float MgOContent { get; set; }
        public float SContent { get; set; }
        public float MnOContent { get; set; }
        public float TiO2Content { get; set; }
    }
}
