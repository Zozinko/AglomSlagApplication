using DBStructure.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DBStructure.DbContexts
{
    public class AuthContext(DbContextOptions<AuthContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshSession> RefreshSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserId);

            modelBuilder.Entity<User>().HasIndex(u => u.UserEmail).IsUnique();
            modelBuilder.Entity<RefreshSession>(session =>
            {
                session.HasKey(s => s.Id);
                session.Property(s => s.TokenHash).HasMaxLength(64);
                session.HasIndex(s => s.TokenHash).IsUnique();
                session.HasIndex(s => new { s.UserId, s.FamilyId });
                session.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
                session.HasOne<RefreshSession>().WithMany().HasForeignKey(s => s.ReplacedById).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
