using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace eST1C_ProgramImporter.Models
{
    public class ProgramHeaders
    {
        [Key]
        public Guid ProgramId { get; set; }
        public string Model { get; set; }
        public string WorkcellName { get; set; }
        public string FilePath { get; set; }
        public DateTime FileDate { get; set; }
        public DateTime DateExtracted { get; set; }
        public ICollection<ProgramDetails> ProgramDetails { get; set; }
    }
}
