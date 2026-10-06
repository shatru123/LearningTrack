using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Day14.EfCoreMigrations;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } // Nullable column added safely
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MigrationHistoryRecord
{
    public string MigrationId { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = "8.0.8";
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MigrationDbContext : DbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Order> Orders => Set<Order>();

    public MigrationDbContext(DbContextOptions<MigrationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(e => e.UserId);
        });
    }
}
