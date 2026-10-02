using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Day11.EfCoreTracking.Tests;

public class Day11Tests
{
    private static TrackingDbContext CreateInMemoryContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TrackingDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task DemonstrateStateTransitions_ShouldFollowExpectedLifecycle()
    {
        // Arrange
        using var context = CreateInMemoryContext(out var connection);
        using (connection)
        {
            var service = new ChangeTrackingService();

            // Act
            var steps = await service.DemonstrateStateTransitionsAsync(
                context, "Alice Smith", "alice@example.com");

            // Assert
            steps.Should().HaveCount(7);
            steps[0].Should().Be(new StateTransitionStep("Instantiation", EntityState.Detached));
            steps[1].Should().Be(new StateTransitionStep("Add", EntityState.Added));
            steps[2].Should().Be(new StateTransitionStep("SaveChanges (Insert)", EntityState.Unchanged));
            steps[3].Should().Be(new StateTransitionStep("Property Mutation", EntityState.Modified));
            steps[4].Should().Be(new StateTransitionStep("SaveChanges (Update)", EntityState.Unchanged));
            steps[5].Should().Be(new StateTransitionStep("Remove", EntityState.Deleted));
            steps[6].Should().Be(new StateTransitionStep("SaveChanges (Delete)", EntityState.Detached));
        }
    }

    [Fact]
    public async Task CompareIdentityResolution_ShouldDemonstrateInstanceReuseDifference()
    {
        // Arrange
        using var context = CreateInMemoryContext(out var connection);
        using (connection)
        {
            var customer = new Customer { Name = "Enterprise Corp", Email = "ops@enterprise.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            context.Orders.AddRange(
                new Order { CustomerId = customer.Id, TotalAmount = 150.00m },
                new Order { CustomerId = customer.Id, TotalAmount = 350.50m },
                new Order { CustomerId = customer.Id, TotalAmount = 99.99m });
            await context.SaveChangesAsync();

            var service = new ChangeTrackingService();

            // Act
            var result = await service.CompareIdentityResolutionAsync(context, customer.Id);

            // Assert
            result.OrderCount.Should().Be(3);

            // AsNoTracking: multiple copies of the same parent entity created on the heap
            result.NoTrackingReferencesAreEqual.Should().BeFalse();
            result.NoTrackingDistinctCustomerInstances.Should().Be(3);

            // AsNoTrackingWithIdentityResolution: single shared instance reused across all child rows
            result.WithIdentityResolutionReferencesAreEqual.Should().BeTrue();
            result.WithIdentityResolutionDistinctCustomerInstances.Should().Be(1);
        }
    }

    [Fact]
    public async Task InspectDetectChanges_ShouldDetectMutationsOnlyWhenInvoked()
    {
        // Arrange
        using var context = CreateInMemoryContext(out var connection);
        using (connection)
        {
            var customer = new Customer { Name = "Original Name", Email = "test@example.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var service = new ChangeTrackingService();

            // Act
            var result = service.InspectDetectChanges(context, customer, "Updated Name");

            // Assert
            result.OriginalValue.Should().Be("Original Name");
            result.CurrentValue.Should().Be("Updated Name");
            result.IsModifiedBeforeDetectChanges.Should().BeFalse();
            result.IsModifiedAfterDetectChanges.Should().BeTrue();
        }
    }

    [Fact]
    public async Task NotificationCustomer_ShouldTrackChangesDirectlyWithoutSnapshotScan()
    {
        // Arrange
        using var context = CreateInMemoryContext(out var connection);
        using (connection)
        {
            var notifCustomer = new NotificationCustomer { Name = "Notification Bob", Email = "bob@example.com" };
            context.NotificationCustomers.Add(notifCustomer);
            await context.SaveChangesAsync();

            context.Entry(notifCustomer).State.Should().Be(EntityState.Unchanged);

            // Act: mutate property directly while AutoDetectChangesEnabled is false
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            notifCustomer.Name = "Notification Bob 2.0";

            // Assert: Notification-based entity informs EF Core directly via INotifyPropertyChanged
            context.Entry(notifCustomer).State.Should().Be(EntityState.Modified);
            context.Entry(notifCustomer).Property(x => x.Name).IsModified.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(new[] { "2", "1", "+", "3", "*" }, 9)]
    [InlineData(new[] { "4", "13", "5", "/", "+" }, 6)]
    [InlineData(new[] { "10", "6", "9", "3", "+", "-11", "*", "/", "*", "17", "+", "5", "+" }, 22)]
    public void EvalRpn_ShouldEvaluateCorrectly(string[] tokens, int expected)
    {
        // Act
        var result = MonotonicStackSolvers.EvalRpn(tokens);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvalRpn_DivisionByZero_ShouldThrowException()
    {
        // Arrange
        var tokens = new[] { "4", "0", "/" };

        // Act & Assert
        FluentActions.Invoking(() => MonotonicStackSolvers.EvalRpn(tokens))
            .Should().Throw<DivideByZeroException>();
    }

    [Theory]
    [InlineData(
        new[] { 73, 74, 75, 71, 69, 72, 76, 73 },
        new[] { 1, 1, 4, 2, 1, 1, 0, 0 })]
    [InlineData(
        new[] { 30, 40, 50, 60 },
        new[] { 1, 1, 1, 0 })]
    [InlineData(
        new[] { 30, 60, 90 },
        new[] { 1, 1, 0 })]
    [InlineData(
        new[] { 90, 80, 70 },
        new[] { 0, 0, 0 })]
    [InlineData(
        new[] { 50 },
        new[] { 0 })]
    [InlineData(
        new[] { 70, 70, 70 },
        new[] { 0, 0, 0 })]
    public void DailyTemperatures_ShouldComputeWaitDaysAccurately(int[] temps, int[] expected)
    {
        // Act
        var result = MonotonicStackSolvers.DailyTemperatures(temps);

        // Assert
        result.Should().Equal(expected);
    }
}
