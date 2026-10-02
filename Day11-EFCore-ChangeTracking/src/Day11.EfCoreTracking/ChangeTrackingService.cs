using Microsoft.EntityFrameworkCore;

namespace Day11.EfCoreTracking;

public record StateTransitionStep(string Action, EntityState State);

public record IdentityResolutionResult(
    int OrderCount,
    bool NoTrackingReferencesAreEqual,
    int NoTrackingDistinctCustomerInstances,
    bool WithIdentityResolutionReferencesAreEqual,
    int WithIdentityResolutionDistinctCustomerInstances);

public record DetectChangesResult(
    bool IsModifiedBeforeDetectChanges,
    bool IsModifiedAfterDetectChanges,
    string OriginalValue,
    string CurrentValue);

public class ChangeTrackingService
{
    /// <summary>
    /// Demonstrates the full lifecycle state transitions of an entity:
    /// Detached -> Added -> Unchanged (post-save) -> Modified -> Deleted -> Detached (post-save).
    /// </summary>
    public async Task<List<StateTransitionStep>> DemonstrateStateTransitionsAsync(
        TrackingDbContext db,
        string customerName,
        string customerEmail)
    {
        var steps = new List<StateTransitionStep>();

        var customer = new Customer { Name = customerName, Email = customerEmail };
        steps.Add(new StateTransitionStep("Instantiation", db.Entry(customer).State)); // Detached

        db.Customers.Add(customer);
        steps.Add(new StateTransitionStep("Add", db.Entry(customer).State)); // Added

        await db.SaveChangesAsync();
        steps.Add(new StateTransitionStep("SaveChanges (Insert)", db.Entry(customer).State)); // Unchanged

        customer.Name = $"{customerName} (Updated)";
        steps.Add(new StateTransitionStep("Property Mutation", db.Entry(customer).State)); // Modified

        await db.SaveChangesAsync();
        steps.Add(new StateTransitionStep("SaveChanges (Update)", db.Entry(customer).State)); // Unchanged

        db.Customers.Remove(customer);
        steps.Add(new StateTransitionStep("Remove", db.Entry(customer).State)); // Deleted

        await db.SaveChangesAsync();
        steps.Add(new StateTransitionStep("SaveChanges (Delete)", db.Entry(customer).State)); // Detached

        return steps;
    }

    /// <summary>
    /// Compares AsNoTracking() vs AsNoTrackingWithIdentityResolution().
    /// AsNoTracking creates duplicate heap instances for duplicate parent entities across a relationship graph.
    /// AsNoTrackingWithIdentityResolution maintains an ephemeral query-level identity map to ensure single instances.
    /// </summary>
    public async Task<IdentityResolutionResult> CompareIdentityResolutionAsync(
        TrackingDbContext db,
        int customerId)
    {
        // 1. AsNoTracking()
        var noTrackingOrders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync();

        var noTrackingRefsEqual = noTrackingOrders.Count >= 2 &&
            ReferenceEquals(noTrackingOrders[0].Customer, noTrackingOrders[1].Customer);

        var noTrackingDistinctInstances = noTrackingOrders
            .Select(o => o.Customer)
            .Where(c => c != null)
            .Distinct(new ReferenceEqualityComparer<Customer?>())
            .Count();

        // 2. AsNoTrackingWithIdentityResolution()
        var withIdentOrders = await db.Orders
            .AsNoTrackingWithIdentityResolution()
            .Include(o => o.Customer)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync();

        var withIdentRefsEqual = withIdentOrders.Count >= 2 &&
            ReferenceEquals(withIdentOrders[0].Customer, withIdentOrders[1].Customer);

        var withIdentDistinctInstances = withIdentOrders
            .Select(o => o.Customer)
            .Where(c => c != null)
            .Distinct(new ReferenceEqualityComparer<Customer?>())
            .Count();

        return new IdentityResolutionResult(
            OrderCount: noTrackingOrders.Count,
            NoTrackingReferencesAreEqual: noTrackingRefsEqual,
            NoTrackingDistinctCustomerInstances: noTrackingDistinctInstances,
            WithIdentityResolutionReferencesAreEqual: withIdentRefsEqual,
            WithIdentityResolutionDistinctCustomerInstances: withIdentDistinctInstances);
    }

    /// <summary>
    /// Demonstrates snapshot change tracking and DetectChanges() behavior when AutoDetectChangesEnabled is false.
    /// </summary>
    public DetectChangesResult InspectDetectChanges(
        TrackingDbContext db,
        Customer customer,
        string updatedName)
    {
        var originalValue = customer.Name;
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            customer.Name = updatedName;

            var propEntry = db.Entry(customer).Property(c => c.Name);
            bool isModifiedBefore = propEntry.IsModified;

            db.ChangeTracker.DetectChanges();
            bool isModifiedAfter = propEntry.IsModified;

            return new DetectChangesResult(
                IsModifiedBeforeDetectChanges: isModifiedBefore,
                IsModifiedAfterDetectChanges: isModifiedAfter,
                OriginalValue: originalValue,
                CurrentValue: customer.Name);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}

public class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class?
{
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T? obj) => obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
