using Microsoft.EntityFrameworkCore;

namespace Day11.EfCoreTracking;

public class TrackingDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<NotificationCustomer> NotificationCustomers => Set<NotificationCustomer>();

    public TrackingDbContext(DbContextOptions<TrackingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(150);

            entity.HasMany(c => c.Orders)
                .WithOne(o => o.Customer)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<NotificationCustomer>(entity =>
        {
            entity.HasKey(nc => nc.Id);
            entity.Property(nc => nc.Name).IsRequired().HasMaxLength(100);
            entity.Property(nc => nc.Email).IsRequired().HasMaxLength(150);

            // Configure entity to use notification-based change tracking instead of snapshot tracking
            entity.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);
        });
    }
}
