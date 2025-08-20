using System;
using System.ComponentModel.DataAnnotations;

namespace eST1C_ProgramImporter.Models
{
    public class ProgramDetails
    {
        [Key]
        public Guid DetailId { get; set; }
        public Guid ProgramId { get; set; }
        public int ExcelRowNum { get; set; }
        public string TorqueUnit { get; set; }
        public string AngleUnit { get; set; }
        public decimal TargetTorque { get; set; }
        public decimal MinAngle { get; set; }
        public decimal MaxAngle { get; set; }
        public int ScrewCount { get; set; }
        public int SpeedRPM { get; set; }
    }
}
