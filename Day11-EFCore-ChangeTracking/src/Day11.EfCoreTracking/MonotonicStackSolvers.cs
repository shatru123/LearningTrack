namespace Day11.EfCoreTracking;

public static class MonotonicStackSolvers
{
    /// <summary>
    /// LeetCode #150: Evaluate Reverse Polish Notation.
    /// Evaluates arithmetic expressions written in Postfix notation using a Stack.
    /// Division truncates toward zero. Time Complexity: O(n), Space Complexity: O(n).
    /// </summary>
    public static int EvalRpn(string[] tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("Tokens array cannot be empty.", nameof(tokens));
        }

        var stack = new Stack<int>();

        foreach (var token in tokens)
        {
            switch (token)
            {
                case "+":
                    stack.Push(stack.Pop() + stack.Pop());
                    break;
                case "-":
                {
                    int b = stack.Pop();
                    int a = stack.Pop();
                    stack.Push(a - b);
                    break;
                }
                case "*":
                    stack.Push(stack.Pop() * stack.Pop());
                    break;
                case "/":
                {
                    int b = stack.Pop();
                    int a = stack.Pop();
                    if (b == 0)
                    {
                        throw new DivideByZeroException("Division by zero in RPN evaluation.");
                    }
                    // C# integer division truncates toward zero
                    stack.Push(a / b);
                    break;
                }
                default:
                    if (int.TryParse(token, out int number))
                    {
                        stack.Push(number);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid token: '{token}'", nameof(tokens));
                    }
                    break;
            }
        }

        if (stack.Count != 1)
        {
            throw new ArgumentException("Invalid RPN expression format.");
        }

        return stack.Pop();
    }

    /// <summary>
    /// LeetCode #739: Daily Temperatures.
    /// Finds the number of days until a warmer temperature using a monotonic decreasing stack of indices.
    /// Time Complexity: O(n), Space Complexity: O(n).
    /// </summary>
    public static int[] DailyTemperatures(int[] temperatures)
    {
        ArgumentNullException.ThrowIfNull(temperatures);
        int n = temperatures.Length;
        var answer = new int[n];
        var stack = new Stack<int>(); // Stores indices of daily temperatures in strictly non-increasing order

        for (int i = 0; i < n; i++)
        {
            while (stack.Count > 0 && temperatures[i] > temperatures[stack.Peek()])
            {
                int prevIndex = stack.Pop();
                answer[prevIndex] = i - prevIndex;
            }

            stack.Push(i);
        }

        return answer;
    }
}
