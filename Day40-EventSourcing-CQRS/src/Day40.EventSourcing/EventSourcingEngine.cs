namespace Day40.EventSourcing;

public class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(string message) : base(message) { }
}

public interface IDomainEvent
{
    string StreamId { get; }
    long Version { get; }
    DateTime OccurredAtUtc { get; }
}

public record AccountOpenedEvent(string AccountId, string OwnerName, decimal InitialBalance, long Version, DateTime OccurredAtUtc) : IDomainEvent
{
    public string StreamId => AccountId;
}

public record FundsDepositedEvent(string AccountId, decimal Amount, long Version, DateTime OccurredAtUtc) : IDomainEvent
{
    public string StreamId => AccountId;
}

public record FundsWithdrawnEvent(string AccountId, decimal Amount, long Version, DateTime OccurredAtUtc) : IDomainEvent
{
    public string StreamId => AccountId;
}

public record AccountClosedEvent(string AccountId, string Reason, long Version, DateTime OccurredAtUtc) : IDomainEvent
{
    public string StreamId => AccountId;
}

public record AccountSnapshot(string AccountId, string OwnerName, decimal Balance, bool IsClosed, long Version);

public class BankAccountAggregate
{
    public string Id { get; private set; } = string.Empty;
    public string OwnerName { get; private set; } = string.Empty;
    public decimal Balance { get; private set; } = 0m;
    public bool IsClosed { get; private set; } = false;
    public long Version { get; private set; } = 0;

    private readonly List<IDomainEvent> _uncommittedEvents = new();
    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents.ToList();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var evt in history)
        {
            Apply(evt);
            Version = evt.Version;
        }
    }

    public void RestoreFromSnapshot(AccountSnapshot snapshot)
    {
        Id = snapshot.AccountId;
        OwnerName = snapshot.OwnerName;
        Balance = snapshot.Balance;
        IsClosed = snapshot.IsClosed;
        Version = snapshot.Version;
    }

    public void Open(string accountId, string ownerName, decimal initialBalance)
    {
        if (Version > 0) throw new InvalidOperationException("Account is already open");
        if (initialBalance < 0) throw new ArgumentException("Initial balance cannot be negative");

        Emit(new AccountOpenedEvent(accountId, ownerName, initialBalance, Version + 1, DateTime.UtcNow));
    }

    public void Deposit(decimal amount)
    {
        if (IsClosed) throw new InvalidOperationException("Cannot deposit to closed account");
        if (amount <= 0) throw new ArgumentException("Deposit must be positive");

        Emit(new FundsDepositedEvent(Id, amount, Version + 1, DateTime.UtcNow));
    }

    public void Withdraw(decimal amount)
    {
        if (IsClosed) throw new InvalidOperationException("Cannot withdraw from closed account");
        if (amount <= 0) throw new ArgumentException("Withdrawal must be positive");
        if (Balance < amount) throw new InvalidOperationException("Insufficient funds");

        Emit(new FundsWithdrawnEvent(Id, amount, Version + 1, DateTime.UtcNow));
    }

    public void Close(string reason)
    {
        if (IsClosed) throw new InvalidOperationException("Account is already closed");
        Emit(new AccountClosedEvent(Id, reason, Version + 1, DateTime.UtcNow));
    }

    private void Emit(IDomainEvent evt)
    {
        Apply(evt);
        Version = evt.Version;
        _uncommittedEvents.Add(evt);
    }

    private void Apply(IDomainEvent evt)
    {
        switch (evt)
        {
            case AccountOpenedEvent opened:
                Id = opened.AccountId;
                OwnerName = opened.OwnerName;
                Balance = opened.InitialBalance;
                IsClosed = false;
                break;
            case FundsDepositedEvent deposited:
                Balance += deposited.Amount;
                break;
            case FundsWithdrawnEvent withdrawn:
                Balance -= withdrawn.Amount;
                break;
            case AccountClosedEvent:
                IsClosed = true;
                break;
        }
    }
}

public class EventStore
{
    private readonly Dictionary<string, List<IDomainEvent>> _streams = new();
    private readonly Dictionary<string, AccountSnapshot> _snapshots = new();
    private readonly object _lock = new();

    public event Action<IDomainEvent>? EventAppended;

    public void AppendEvents(string streamId, long expectedVersion, IEnumerable<IDomainEvent> newEvents)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
            {
                stream = new List<IDomainEvent>();
                _streams[streamId] = stream;
            }

            long currentVersion = stream.Count > 0 ? stream[^1].Version : 0;
            if (currentVersion != expectedVersion)
            {
                throw new OptimisticConcurrencyException(
                    $"Stream '{streamId}' concurrency conflict. Expected version {expectedVersion}, actual {currentVersion}.");
            }

            foreach (var evt in newEvents)
            {
                stream.Add(evt);
                EventAppended?.Invoke(evt);
            }
        }
    }

    public void SaveSnapshot(string streamId, AccountSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshots[streamId] = snapshot;
        }
    }

    public BankAccountAggregate LoadAggregate(string streamId)
    {
        lock (_lock)
        {
            var aggregate = new BankAccountAggregate();
            long fromVersion = 0;

            if (_snapshots.TryGetValue(streamId, out var snapshot))
            {
                aggregate.RestoreFromSnapshot(snapshot);
                fromVersion = snapshot.Version;
            }

            if (_streams.TryGetValue(streamId, out var stream))
            {
                var subsequentEvents = stream.Where(e => e.Version > fromVersion);
                aggregate.LoadFromHistory(subsequentEvents);
            }

            return aggregate;
        }
    }
}

public record AccountSummaryView(string AccountId, string OwnerName, decimal CurrentBalance, int TransactionCount, string Status);

public class AccountProjectionManager
{
    private readonly Dictionary<string, AccountSummaryView> _readViews = new();
    private readonly object _lock = new();

    public AccountProjectionManager(EventStore eventStore)
    {
        eventStore.EventAppended += Project;
    }

    public AccountSummaryView? GetAccountView(string accountId)
    {
        lock (_lock)
        {
            return _readViews.TryGetValue(accountId, out var view) ? view : null;
        }
    }

    private void Project(IDomainEvent evt)
    {
        lock (_lock)
        {
            switch (evt)
            {
                case AccountOpenedEvent opened:
                    _readViews[opened.AccountId] = new AccountSummaryView(
                        opened.AccountId,
                        opened.OwnerName,
                        opened.InitialBalance,
                        TransactionCount: 1,
                        Status: "Active"
                    );
                    break;
                case FundsDepositedEvent deposited:
                    if (_readViews.TryGetValue(deposited.AccountId, out var currentDep))
                    {
                        _readViews[deposited.AccountId] = currentDep with
                        {
                            CurrentBalance = currentDep.CurrentBalance + deposited.Amount,
                            TransactionCount = currentDep.TransactionCount + 1
                        };
                    }
                    break;
                case FundsWithdrawnEvent withdrawn:
                    if (_readViews.TryGetValue(withdrawn.AccountId, out var currentWd))
                    {
                        _readViews[withdrawn.AccountId] = currentWd with
                        {
                            CurrentBalance = currentWd.CurrentBalance - withdrawn.Amount,
                            TransactionCount = currentWd.TransactionCount + 1
                        };
                    }
                    break;
                case AccountClosedEvent closed:
                    if (_readViews.TryGetValue(closed.AccountId, out var currentCl))
                    {
                        _readViews[closed.AccountId] = currentCl with
                        {
                            Status = "Closed",
                            TransactionCount = currentCl.TransactionCount + 1
                        };
                    }
                    break;
            }
        }
    }
}
