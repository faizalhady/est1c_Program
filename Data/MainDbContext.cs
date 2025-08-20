using eST1C_ProgramImporter.Models;
using Microsoft.EntityFrameworkCore;

namespace eST1C_ProgramImporter.Data
{
    public class MainDbContext : DbContext
    {
        public DbSet<ProgramHeaders> Program { get; set; }
        public DbSet<ProgramDetails> ProgramDetails { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=mypenm0iesvr02\SQLEXPRESS;Database=EST1C;Trusted_Connection=True;TrustServerCertificate=True;");
        }

      protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ProgramHeaders>()
        .ToTable("Program") // 👈 Map to correct table
        .HasKey(p => p.ProgramId);

    modelBuilder.Entity<ProgramDetails>()
        .ToTable("ProgramDetails") // 👈 Map to correct table
        .HasKey(d => d.DetailId);

    modelBuilder.Entity<ProgramDetails>()
        .HasOne<ProgramHeaders>() // 👈 Relation: each detail has one program
        .WithMany(p => p.ProgramDetails) // 👈 Program has many details
        .HasForeignKey(d => d.ProgramId) // 👈 Explicit FK
        .OnDelete(DeleteBehavior.Cascade); // 👈 Matches your SQL ON DELETE CASCADE

    base.OnModelCreating(modelBuilder);
}


    }
}
