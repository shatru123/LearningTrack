using Xunit;

namespace Day40.EventSourcing.Tests;

public class Day40Tests
{
    [Fact]
    public void BankAccountAggregate_DepositAndWithdraw_CalculatesCorrectBalance()
    {
        var account = new BankAccountAggregate();
        account.Open("acc-101", "Alice", 100m);
        account.Deposit(50m);
        account.Withdraw(30m);

        Assert.Equal("acc-101", account.Id);
        Assert.Equal("Alice", account.OwnerName);
        Assert.Equal(120m, account.Balance);
        Assert.Equal(3, account.Version);
        Assert.Equal(3, account.UncommittedEvents.Count);
    }

    [Fact]
    public void BankAccountAggregate_WithdrawExceedingBalance_ThrowsInvalidOperationException()
    {
        var account = new BankAccountAggregate();
        account.Open("acc-102", "Bob", 50m);

        var ex = Assert.Throws<InvalidOperationException>(() => account.Withdraw(100m));
        Assert.Contains("Insufficient funds", ex.Message);
    }

    [Fact]
    public void EventStore_AppendAndLoadAggregate_ReconstructsStateFromEvents()
    {
        var store = new EventStore();
        var account = new BankAccountAggregate();

        account.Open("acc-103", "Charlie", 200m);
        account.Deposit(100m);
        account.Withdraw(50m);

        store.AppendEvents(account.Id, expectedVersion: 0, account.UncommittedEvents);

        // Load fresh aggregate purely from event history replay
        var rehydrated = store.LoadAggregate("acc-103");

        Assert.Equal("acc-103", rehydrated.Id);
        Assert.Equal("Charlie", rehydrated.OwnerName);
        Assert.Equal(250m, rehydrated.Balance);
        Assert.Equal(3, rehydrated.Version);
        Assert.Empty(rehydrated.UncommittedEvents);
    }

    [Fact]
    public void EventStore_OptimisticConcurrencyConflict_ThrowsException()
    {
        var store = new EventStore();
        var account = new BankAccountAggregate();
        account.Open("acc-104", "David", 50m);
        store.AppendEvents("acc-104", expectedVersion: 0, account.UncommittedEvents);

        // Another thread attempts to append assuming version 0 when version is already 1
        var conflictingAccount = new BankAccountAggregate();
        conflictingAccount.Open("acc-104", "David Duplicate", 100m);

        Assert.Throws<OptimisticConcurrencyException>(() =>
        {
            store.AppendEvents("acc-104", expectedVersion: 0, conflictingAccount.UncommittedEvents);
        });
    }

    [Fact]
    public void EventStore_SnapshotRestore_AcceleratesReplayFromSnapshot()
    {
        var store = new EventStore();
        string accId = "acc-105";

        var acc = new BankAccountAggregate();
        acc.Open(accId, "Eve", 1000m);
        acc.Deposit(500m); // Version 2, Balance 1500
        store.AppendEvents(accId, 0, acc.UncommittedEvents);
        acc.ClearUncommittedEvents();

        // Create snapshot at Version 2
        var snapshot = new AccountSnapshot(acc.Id, acc.OwnerName, acc.Balance, acc.IsClosed, acc.Version);
        store.SaveSnapshot(accId, snapshot);

        // Subsequent events after snapshot
        acc.Deposit(200m); // Version 3
        acc.Withdraw(100m); // Version 4
        store.AppendEvents(accId, expectedVersion: 2, acc.UncommittedEvents);

        // Load aggregate: should restore snapshot (v=2, 1500), then replay v=3 and v=4
        var rehydrated = store.LoadAggregate(accId);

        Assert.Equal(1600m, rehydrated.Balance);
        Assert.Equal(4, rehydrated.Version);
    }

    [Fact]
    public void AccountProjectionManager_MaterializesCQRSReadView()
    {
        var store = new EventStore();
        var projection = new AccountProjectionManager(store);

        var acc = new BankAccountAggregate();
        acc.Open("acc-106", "Frank", 500m);
        acc.Deposit(250m);
        acc.Withdraw(100m);
        acc.Close("Customer request");

        store.AppendEvents(acc.Id, 0, acc.UncommittedEvents);

        var view = projection.GetAccountView("acc-106");

        Assert.NotNull(view);
        Assert.Equal("acc-106", view.AccountId);
        Assert.Equal("Frank", view.OwnerName);
        Assert.Equal(650m, view.CurrentBalance);
        Assert.Equal(4, view.TransactionCount);
        Assert.Equal("Closed", view.Status);
    }

    [Fact]
    public void PalindromePartitioning_ExampleString_ReturnsAllValidPartitions()
    {
        string s = "aab";
        var partitions = PalindromePartitioningSolver.Partition(s);

        // Expected: [["a","a","b"], ["aa","b"]]
        Assert.Equal(2, partitions.Count);
        Assert.Contains(partitions, p => p.SequenceEqual(["a", "a", "b"]));
        Assert.Contains(partitions, p => p.SequenceEqual(["aa", "b"]));
    }

    [Fact]
    public void PalindromePartitioning_SingleCharacterString_ReturnsSinglePartition()
    {
        var partitions = PalindromePartitioningSolver.Partition("a");

        Assert.Single(partitions);
        Assert.Equal(["a"], partitions[0]);
    }

    [Fact]
    public void PalindromePartitioning_AllSameCharacters_ReturnsAllCombinations()
    {
        string s = "aaa";
        var partitions = PalindromePartitioningSolver.Partition(s);

        // For "aaa": ["a","a","a"], ["a","aa"], ["aa","a"], ["aaa"] -> 4
        Assert.Equal(4, partitions.Count);
        Assert.Contains(partitions, p => p.SequenceEqual(["a", "a", "a"]));
        Assert.Contains(partitions, p => p.SequenceEqual(["aaa"]));
    }

    [Fact]
    public void PalindromePartitioning_NoPalindromesBeyondSingleChars_ReturnsSingleCharacterList()
    {
        string s = "abc";
        var partitions = PalindromePartitioningSolver.Partition(s);

        Assert.Single(partitions);
        Assert.Equal(["a", "b", "c"], partitions[0]);
    }
}
