using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Day29.RabbitMQCoreExchanges;

public enum ExchangeType
{
    Direct,
    Topic,
    Fanout,
    Headers
}

public enum HeadersMatchMode
{
    All,
    Any
}

public record AmqpMessage(
    ulong DeliveryTag,
    string MessageId,
    string RoutingKey,
    string Payload,
    IReadOnlyDictionary<string, object> Headers,
    bool Redelivered,
    DateTime TimestampUtc);

public class QueueBinding
{
    public string QueueName { get; }
    public string RoutingKeyPattern { get; }
    public IReadOnlyDictionary<string, object>? HeaderFilter { get; }
    public HeadersMatchMode MatchMode { get; }

    public QueueBinding(
        string queueName,
        string routingKeyPattern = "",
        IReadOnlyDictionary<string, object>? headerFilter = null,
        HeadersMatchMode matchMode = HeadersMatchMode.All)
    {
        QueueName = queueName;
        RoutingKeyPattern = routingKeyPattern;
        HeaderFilter = headerFilter;
        MatchMode = matchMode;
    }
}

public class AmqpQueue
{
    public string Name { get; }
    public string? DeadLetterExchange { get; }
    public string? DeadLetterRoutingKey { get; }
    public int PrefetchCount { get; set; } = 10;

    private readonly ConcurrentQueue<AmqpMessage> _messages = new();
    private readonly ConcurrentDictionary<ulong, AmqpMessage> _unackedMessages = new();

    public int ReadyMessageCount => _messages.Count;
    public int UnackedMessageCount => _unackedMessages.Count;

    public AmqpQueue(string name, string? deadLetterExchange = null, string? deadLetterRoutingKey = null)
    {
        Name = name;
        DeadLetterExchange = deadLetterExchange;
        DeadLetterRoutingKey = deadLetterRoutingKey;
    }

    public void Enqueue(AmqpMessage message, bool toFront = false)
    {
        if (toFront)
        {
            // Requeue to head: snapshot, clear, push redelivered, restore remaining
            var list = _messages.ToList();
            while (_messages.TryDequeue(out _)) { }
            _messages.Enqueue(message);
            foreach (var item in list)
            {
                _messages.Enqueue(item);
            }
        }
        else
        {
            _messages.Enqueue(message);
        }
    }

    public AmqpMessage? Dequeue()
    {
        if (_unackedMessages.Count >= PrefetchCount)
        {
            // QoS Prefetch limit reached: hold message delivery until ACK/NACK
            return null;
        }

        if (_messages.TryDequeue(out var msg))
        {
            _unackedMessages[msg.DeliveryTag] = msg;
            return msg;
        }
        return null;
    }

    public bool Ack(ulong deliveryTag)
    {
        return _unackedMessages.TryRemove(deliveryTag, out _);
    }

    public AmqpMessage? Nack(ulong deliveryTag, bool requeue)
    {
        if (!_unackedMessages.TryRemove(deliveryTag, out var msg))
            return null;

        if (requeue)
        {
            Enqueue(msg with { Redelivered = true }, toFront: true);
        }

        return msg;
    }
}

/// <summary>
/// High-fidelity in-memory AMQP 0-9-1 Message Broker Simulator.
/// Simulates Direct, Topic, Fanout, and Headers exchanges, Publisher Confirms,
/// Consumer Ack/Nack, Dead Letter Exchanges (DLX), and Prefetch QoS.
/// </summary>
public class AmqpBrokerSimulator
{
    private readonly ConcurrentDictionary<string, (ExchangeType Type, List<QueueBinding> Bindings)> _exchanges = new();
    private readonly ConcurrentDictionary<string, AmqpQueue> _queues = new();
    private ulong _deliveryTagSequence;

    public int PublishedCount { get; private set; }
    public int AckedCount { get; private set; }
    public int NackedCount { get; private set; }
    public int DeadLetteredCount { get; private set; }

    public void DeclareExchange(string name, ExchangeType type)
    {
        _exchanges.TryAdd(name, (type, new List<QueueBinding>()));
    }

    public void DeclareQueue(string name, string? deadLetterExchange = null, string? deadLetterRoutingKey = null, int prefetchCount = 10)
    {
        var queue = new AmqpQueue(name, deadLetterExchange, deadLetterRoutingKey) { PrefetchCount = prefetchCount };
        _queues.TryAdd(name, queue);
    }

    public void BindQueue(
        string queueName,
        string exchangeName,
        string routingKey = "",
        IReadOnlyDictionary<string, object>? headers = null,
        HeadersMatchMode matchMode = HeadersMatchMode.All)
    {
        if (!_exchanges.TryGetValue(exchangeName, out var exchange))
            throw new InvalidOperationException($"Exchange '{exchangeName}' does not exist.");
        if (!_queues.ContainsKey(queueName))
            throw new InvalidOperationException($"Queue '{queueName}' does not exist.");

        lock (exchange.Bindings)
        {
            exchange.Bindings.Add(new QueueBinding(queueName, routingKey, headers, matchMode));
        }
    }

    /// <summary>
    /// Publishes a message to an exchange with Publisher Confirms semantics.
    /// Returns true (ACK) if successfully delivered to matching queues, or false (NACK) if unrouted on mandatory.
    /// </summary>
    public bool Publish(
        string exchangeName,
        string routingKey,
        string payload,
        IReadOnlyDictionary<string, object>? headers = null,
        bool mandatory = false)
    {
        if (!_exchanges.TryGetValue(exchangeName, out var exchange))
            throw new InvalidOperationException($"Exchange '{exchangeName}' not declared.");

        headers ??= new Dictionary<string, object>();
        var matchingQueueNames = GetMatchingQueues(exchange.Type, exchange.Bindings, routingKey, headers);

        if (matchingQueueNames.Count == 0 && mandatory)
        {
            return false; // Publisher confirm NACK for unroutable mandatory message
        }

        var tag = Interlocked.Increment(ref _deliveryTagSequence);
        var message = new AmqpMessage(
            DeliveryTag: tag,
            MessageId: Guid.NewGuid().ToString("N"),
            RoutingKey: routingKey,
            Payload: payload,
            Headers: headers,
            Redelivered: false,
            TimestampUtc: DateTime.UtcNow);

        foreach (var qName in matchingQueueNames)
        {
            if (_queues.TryGetValue(qName, out var queue))
            {
                queue.Enqueue(message);
            }
        }

        PublishedCount++;
        return true; // Publisher confirm ACK
    }

    public AmqpMessage? BasicGet(string queueName)
    {
        if (!_queues.TryGetValue(queueName, out var queue))
            return null;

        return queue.Dequeue();
    }

    public bool BasicAck(string queueName, ulong deliveryTag)
    {
        if (_queues.TryGetValue(queueName, out var queue))
        {
            var success = queue.Ack(deliveryTag);
            if (success) AckedCount++;
            return success;
        }
        return false;
    }

    public bool BasicNack(string queueName, ulong deliveryTag, bool requeue)
    {
        if (!_queues.TryGetValue(queueName, out var queue))
            return false;

        var message = queue.Nack(deliveryTag, requeue);
        if (message == null) return false;

        NackedCount++;

        if (!requeue && !string.IsNullOrEmpty(queue.DeadLetterExchange))
        {
            // Route to Dead Letter Exchange (DLX)
            DeadLetteredCount++;
            var dlxRoutingKey = queue.DeadLetterRoutingKey ?? message.RoutingKey;
            Publish(queue.DeadLetterExchange, dlxRoutingKey, message.Payload, message.Headers);
        }

        return true;
    }

    public AmqpQueue? GetQueue(string name)
    {
        _queues.TryGetValue(name, out var queue);
        return queue;
    }

    private HashSet<string> GetMatchingQueues(
        ExchangeType type,
        List<QueueBinding> bindings,
        string routingKey,
        IReadOnlyDictionary<string, object> headers)
    {
        var matched = new HashSet<string>();
        lock (bindings)
        {
            foreach (var b in bindings)
            {
                if (IsBindingMatch(type, b, routingKey, headers))
                {
                    matched.Add(b.QueueName);
                }
            }
        }
        return matched;
    }

    private bool IsBindingMatch(
        ExchangeType type,
        QueueBinding binding,
        string routingKey,
        IReadOnlyDictionary<string, object> headers)
    {
        return type switch
        {
            ExchangeType.Fanout => true,
            ExchangeType.Direct => string.Equals(binding.RoutingKeyPattern, routingKey, StringComparison.Ordinal),
            ExchangeType.Topic => MatchTopicPattern(binding.RoutingKeyPattern, routingKey),
            ExchangeType.Headers => MatchHeaders(binding.HeaderFilter, headers, binding.MatchMode),
            _ => false
        };
    }

    private static bool MatchTopicPattern(string pattern, string routingKey)
    {
        // AMQP 0-9-1 Topic Exchange rules:
        // * matches exactly one word
        // # matches zero or more words
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", "[^.]+")
            .Replace("\\#", ".*") + "$";

        return Regex.IsMatch(routingKey, regexPattern);
    }

    private static bool MatchHeaders(
        IReadOnlyDictionary<string, object>? filter,
        IReadOnlyDictionary<string, object> headers,
        HeadersMatchMode mode)
    {
        if (filter == null || filter.Count == 0) return true;

        if (mode == HeadersMatchMode.All)
        {
            foreach (var (k, v) in filter)
            {
                if (!headers.TryGetValue(k, out var actual) || !object.Equals(v, actual))
                    return false;
            }
            return true;
        }
        else // Any
        {
            foreach (var (k, v) in filter)
            {
                if (headers.TryGetValue(k, out var actual) && object.Equals(v, actual))
                    return true;
            }
            return false;
        }
    }
}
