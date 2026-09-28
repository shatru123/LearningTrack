using System.Runtime.CompilerServices;

namespace Day03.AsyncStateMachine;

/// <summary>
/// Architectural inspection of what the C# Roslyn compiler generates behind the scenes
/// when an engineer writes an async method:
///
/// public async Task&lt;int&gt; FetchTicketCountAsync(int eventId)
/// {
///     await Task.Delay(10);
///     return eventId * 2;
/// }
/// </summary>
public static class AsyncStateMachineDissection
{
    /// <summary>
    /// Explicit manual reproduction of the compiler-generated struct state machine.
    /// Implements IAsyncStateMachine.
    /// </summary>
    public struct ManualTicketStateMachine : IAsyncStateMachine
    {
        // 1. State variable:
        // -1 = Not started / running
        //  0 = Yielded at first await
        // -2 = Completed
        public int State;

        // 2. AsyncTaskMethodBuilder manages the return Task and ThreadPool dispatching
        public AsyncTaskMethodBuilder<int> Builder;

        // 3. Hoisted parameters and locals
        public int EventId;
        private TaskAwaiter _awaiter;

        public void MoveNext()
        {
            int result;
            try
            {
                if (State != 0)
                {
                    // Code before first await
                    Task delayTask = Task.Delay(10);
                    TaskAwaiter awaiter = delayTask.GetAwaiter();

                    if (!awaiter.IsCompleted)
                    {
                        State = 0;
                        _awaiter = awaiter;

                        // Hooks continuation to resume MoveNext when awaiter completes
                        Builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                        return;
                    }
                }
                else
                {
                    // Code resuming continuation after await
                    _awaiter.GetResult();
                }

                // Completion logic
                result = EventId * 2;
            }
            catch (Exception ex)
            {
                State = -2;
                Builder.SetException(ex);
                return;
            }

            State = -2;
            Builder.SetResult(result);
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            Builder.SetStateMachine(stateMachine);
        }
    }

    /// <summary>
    /// Method entry point matching compiler output: initializes the struct state machine
    /// and invokes Builder.Start(ref stateMachine).
    /// </summary>
    public static Task<int> ExecuteManualStateMachineAsync(int eventId)
    {
        var stateMachine = new ManualTicketStateMachine
        {
            EventId = eventId,
            Builder = AsyncTaskMethodBuilder<int>.Create(),
            State = -1
        };

        stateMachine.Builder.Start(ref stateMachine);
        return stateMachine.Builder.Task;
    }

    /// <summary>
    /// Demonstrates TaskCompletionSource&lt;T&gt; to adapt callback/event-based asynchronous systems
    /// into awaitable Task pipelines without blocking threads.
    /// </summary>
    public static Task<string> FromLegacyCallbackAsync(Action<Action<string>> trigger)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            trigger(result =>
            {
                tcs.TrySetResult(result);
            });
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }
}
