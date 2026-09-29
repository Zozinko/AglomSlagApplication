using DBStructure.Entities.SlagMode;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DBStructure.DbContexts
{
    public class SlagModeContext : DbContext
    {
        public SlagModeContext(DbContextOptions<SlagModeContext> options) : base(options)
        {
        }

        public DbSet<BlastFurnace> BlastFurnaces { get; set; }
        public DbSet<CalcVariant> CalcVariants { get; set; }
        public DbSet<ComponentGuide> ComponentsGuide { get; set; }
        public DbSet<ComponentVariant> ComponentVariants { get; set; }
        public DbSet<VariantRelation> VariantRelations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Keys
            modelBuilder.Entity<BlastFurnace>()
                .HasKey(bf => bf.BFId);

            modelBuilder.Entity<CalcVariant>()
                .HasKey(cv => cv.VariantId);

            modelBuilder.Entity<ComponentGuide>()
                .HasKey(cg => cg.ComponentId);

            modelBuilder.Entity<ComponentVariant>()
                .HasKey(cv => new { cv.VariantId, cv.ComponentId });

            modelBuilder.Entity<VariantRelation>()
                .HasKey(vr => new { vr.ParentId, vr.ChildId });
            #endregion

            #region Relations
            modelBuilder.Entity<CalcVariant>()
                .HasOne(e => e.BlastFurnace)
                .WithMany(bf => bf.CalcVariants)
                .OnDelete(DeleteBehavior.Cascade)
                .HasForeignKey(cv => cv.BFId);

            modelBuilder.Entity<ComponentVariant>()
                .HasMany(cv => cv.CalcVariants)
                .WithMany(cv => cv.ComponentVariants);

            modelBuilder.Entity<VariantRelation>()
                .HasMany(vr => vr.VariantParents)
                .WithMany(vr => vr.ChildRelation);
            #endregion

        }
    }
}
