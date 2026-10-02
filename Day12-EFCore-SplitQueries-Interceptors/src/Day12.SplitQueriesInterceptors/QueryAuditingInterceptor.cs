using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Day12.SplitQueriesInterceptors;

public record CommandAuditLog(
    string CommandText,
    long DurationMilliseconds,
    bool IsSlowQuery,
    DateTime ExecutedAtUtc);

public class QueryAuditingInterceptor : DbCommandInterceptor
{
    private readonly long _slowQueryThresholdMs;
    private readonly ConcurrentBag<CommandAuditLog> _logs = new();
    private readonly Action<CommandAuditLog>? _onQueryLogged;

    public QueryAuditingInterceptor(long slowQueryThresholdMs = 100, Action<CommandAuditLog>? onQueryLogged = null)
    {
        _slowQueryThresholdMs = slowQueryThresholdMs;
        _onQueryLogged = onQueryLogged;
    }

    public IReadOnlyList<CommandAuditLog> Logs => _logs.ToArray();

    public int ExecutedCommandCount => _logs.Count;

    public void ClearLogs() => _logs.Clear();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        RecordExecution(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        RecordExecution(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result)
    {
        return base.CommandCreated(eventData, result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        RecordExecution(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        RecordExecution(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        RecordExecution(command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        RecordExecution(command, eventData.Duration);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void RecordExecution(DbCommand command, TimeSpan duration)
    {
        var elapsedMs = (long)duration.TotalMilliseconds;
        var isSlow = elapsedMs >= _slowQueryThresholdMs;

        var log = new CommandAuditLog(
            CommandText: command.CommandText,
            DurationMilliseconds: elapsedMs,
            IsSlowQuery: isSlow,
            ExecutedAtUtc: DateTime.UtcNow);

        _logs.Add(log);
        _onQueryLogged?.Invoke(log);
    }
}
