using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Day11.EfCoreTracking;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public int Id { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
}

/// <summary>
/// Demonstrates notification-based change tracking via INotifyPropertyChanged and INotifyPropertyChanging.
/// EF Core can track changes immediately when properties are set without requiring snapshot comparisons during DetectChanges().
/// </summary>
public class NotificationCustomer : INotifyPropertyChanged, INotifyPropertyChanging
{
    private int _id;
    private string _name = string.Empty;
    private string _email = string.Empty;

    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                OnPropertyChanging();
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                OnPropertyChanging();
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            if (_email != value)
            {
                OnPropertyChanging();
                _email = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected void OnPropertyChanging([CallerMemberName] string? propertyName = null)
        => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
}
