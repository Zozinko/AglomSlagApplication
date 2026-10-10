using DBStructure.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DBStructure.DbContexts
{
    public class AuthContext(DbContextOptions<AuthContext> options) : DbContext(options)
    {
        DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserId);

            modelBuilder.Entity<User>().HasData(new User
            {
                PasswordHash = "Password",
                UserEmail = "test@test.test",
                UserName = "test",
                UserId = -1
            });
        }
    }
}
