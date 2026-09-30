using DBStructure.Entities.SlagMode;
using Microsoft.EntityFrameworkCore;

namespace DBStructure.DbContexts
{
    public class SlagModeContext : DbContext
    {
        public SlagModeContext(DbContextOptions<SlagModeContext> options) : base(options) { }

        public DbSet<BlastFurnace> BlastFurnaces { get; set; }
        public DbSet<CalcVariant> CalcVariants { get; set; }
        public DbSet<ComponentGuide> ComponentsGuide { get; set; }
        public DbSet<ComponentVariant> ComponentVariants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Keys
            modelBuilder.Entity<BlastFurnace>().HasKey(bf => bf.BFId);
            modelBuilder.Entity<CalcVariant>().HasKey(cv => cv.VariantId);
            modelBuilder.Entity<ComponentGuide>().HasKey(cg => cg.ComponentId);
            modelBuilder.Entity<ComponentVariant>().HasKey(cv => new { cv.VariantId, cv.ComponentId });
            #endregion

            #region Relations
            
            // 1. CalcVariant -> BlastFurnace
            modelBuilder.Entity<CalcVariant>()
                .HasOne(cv => cv.BlastFurnace)
                .WithMany(bf => bf.CalcVariants)
                .HasForeignKey(cv => cv.BFId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Связь CalcVariant (Parent -> Children)
            modelBuilder.Entity<CalcVariant>()
                .HasOne(cv => cv.Parent)
                .WithMany(cv => cv.Children)
                .HasForeignKey(cv => cv.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. ComponentVariant -> CalcVariant
            modelBuilder.Entity<ComponentVariant>()
                .HasOne(cv => cv.CalcVariant)
                .WithMany(c => c.ComponentVariants)
                .HasForeignKey(cv => cv.VariantId)
                .OnDelete(DeleteBehavior.Cascade);

            // 4. ComponentVariant -> ComponentGuide
            modelBuilder.Entity<ComponentVariant>()
                .HasOne(cv => cv.ComponentGuide)
                .WithMany()
                .HasForeignKey(cv => cv.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion
        }
    }
}