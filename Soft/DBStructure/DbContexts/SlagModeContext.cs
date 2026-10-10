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
            
            #region Data
            // 1. Доменные печи
            modelBuilder.Entity<BlastFurnace>().HasData(
                new BlastFurnace
                {
                    BFId = -1,
                    BfName = "TestBF"
                }
            );

            // 2. Справочник компонентов
            modelBuilder.Entity<ComponentGuide>().HasData(
                new ComponentGuide { ComponentId = -1, UserId = -1, ComponentName = "агломерат а/ф № 2 и 3", FeContent = 58.5f, SiO2Content = 5.88f, Al2O3Content = 1.75f, CaOContent = 8.72f, MgOContent = 1.63f, SContent = 0.028f, MnOContent = 0.19f, TiO2Content = 0.24f },
                new ComponentGuide { ComponentId = -2, UserId = -1, ComponentName = "агломерат а/ф № 4", FeContent = 58.3f, SiO2Content = 5.95f, Al2O3Content = 1.76f, CaOContent = 8.86f, MgOContent = 1.64f, SContent = 0.028f, MnOContent = 0.19f, TiO2Content = 0.24f },
                new ComponentGuide { ComponentId = -3, UserId = -1, ComponentName = "окатыши ССГПО", FeContent = 62.6f, SiO2Content = 3.7f, Al2O3Content = 1.21f, CaOContent = 4.02f, MgOContent = 0.99f, SContent = 0.067f, MnOContent = 0.16f, TiO2Content = 0.32f },
                new ComponentGuide { ComponentId = -4, UserId = -1, ComponentName = "ЛебГОК", FeContent = 65.7f, SiO2Content = 5.17f, Al2O3Content = 0.25f, CaOContent = 0.4f, MgOContent = 0.22f, SContent = 0.01f, MnOContent = 0.05f, TiO2Content = 0f },
                new ComponentGuide { ComponentId = -5, UserId = -1, ComponentName = "КачГОК", FeContent = 60.4f, SiO2Content = 4.36f, Al2O3Content = 2.59f, CaOContent = 1.28f, MgOContent = 2.9f, SContent = 0.02f, MnOContent = 0.23f, TiO2Content = 2.66f },
                new ComponentGuide { ComponentId = -6, UserId = -1, ComponentName = "МихГОК", FeContent = 63.3f, SiO2Content = 7.25f, Al2O3Content = 0.23f, CaOContent = 1.49f, MgOContent = 0.25f, SContent = 0.01f, MnOContent = 0.04f, TiO2Content = 0f },
                new ComponentGuide { ComponentId = -7, UserId = -1, ComponentName = "Сварочный шлак", FeContent = 69.8f, SiO2Content = 4.3f, Al2O3Content = 0.74f, CaOContent = 0.25f, MgOContent = 0.39f, SContent = 0.02f, MnOContent = 0.84f, TiO2Content = 0f },
                new ComponentGuide { ComponentId = -8, UserId = -1, ComponentName = "Королёк", FeContent = 68.4f, SiO2Content = 6.8f, Al2O3Content = 2f, CaOContent = 8.5f, MgOContent = 3.1f, SContent = 0.11f, MnOContent = 0.98f, TiO2Content = 0.34f }
            );

            // 3. Варианты расчета
            modelBuilder.Entity<CalcVariant>().HasData(
                new 
                {
                    VariantId = -1,
                    BFId = -1,
                    CalcDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    SaveTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    VariantName = "-1st variant",
                    IsImport = false,
                    // Coke
                    CokeConsumption = 419.8f,
                    CokeSContent = 0.428f,
                    CokeAshAmount = 12.7f,
                    CokeAshCaOContent = 7.8f,
                    CokeAshSiO2Content = 48.1f,
                    CokeAshAl2O3Content = 24.6f,
                    CokeAshMgOContent = 2f,
                    // Slag
                    SlagCaOContent = 40.9f,
                    SlagSiO2Content = 36.56f,
                    SlagTiO2Content = 0.01f,
                    // Castiron
                    CiTemperature = 1450f,
                    CiSiContent = 0.512f,
                    CiSContent = 0.016f,
                    CiMnContent = 0.2f,
                    CiCContent = 4.702f,
                    CiTiContent = 0f,
                    CICrContent = 0f
                }
            );

            // 4. Компоненты в варианте расчета
            modelBuilder.Entity<ComponentVariant>().HasData(
                new { VariantId = -1, ComponentId = -1, ComponentName = "агломерат а/ф № 2 и 3", Consumption = 441.5f, FeContent = 58.5f, SiO2Content = 5.88f, Al2O3Content = 1.75f, CaOContent = 8.72f, MgOContent = 1.63f, SContent = 0.028f, MnOContent = 0.19f, TiO2Content = 0.24f },
                new { VariantId = -1, ComponentId = -2, ComponentName = "агломерат а/ф № 4", Consumption = 485.9f, FeContent = 58.3f, SiO2Content = 5.95f, Al2O3Content = 1.76f, CaOContent = 8.86f, MgOContent = 1.64f, SContent = 0.028f, MnOContent = 0.19f, TiO2Content = 0.24f },
                new { VariantId = -1, ComponentId = -3, ComponentName = "окатыши ССГПО", Consumption = 568.7f, FeContent = 62.6f, SiO2Content = 3.7f, Al2O3Content = 1.21f, CaOContent = 4.02f, MgOContent = 0.99f, SContent = 0.067f, MnOContent = 0.16f, TiO2Content = 0.32f },
                new { VariantId = -1, ComponentId = -4, ComponentName = "ЛебГОК", Consumption = 54.7f, FeContent = 65.7f, SiO2Content = 5.17f, Al2O3Content = 0.25f, CaOContent = 0.4f, MgOContent = 0.22f, SContent = 0.01f, MnOContent = 0.05f, TiO2Content = 0f },
                new { VariantId = -1, ComponentId = -5, ComponentName = "КачГОК", Consumption = 54.1f, FeContent = 60.4f, SiO2Content = 4.36f, Al2O3Content = 2.59f, CaOContent = 1.28f, MgOContent = 2.9f, SContent = 0.02f, MnOContent = 0.23f, TiO2Content = 2.66f },
                new { VariantId = -1, ComponentId = -6, ComponentName = "МихГОК", Consumption = 48.5f, FeContent = 63.3f, SiO2Content = 7.25f, Al2O3Content = 0.23f, CaOContent = 1.49f, MgOContent = 0.25f, SContent = 0.01f, MnOContent = 0.04f, TiO2Content = 0f }
            );
            #endregion
        }
    }
}