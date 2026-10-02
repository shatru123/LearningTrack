namespace Day10.TransactionsAcid;

public static class ValidParenthesesSolver
{
    /// <summary>
    /// LeetCode #20: Valid Parentheses
    /// Determines if an input string composed of '()[]{}' is valid.
    /// Time Complexity: O(N)
    /// Space Complexity: O(N)
    /// </summary>
    public static bool IsValid(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;
        if (s.Length % 2 != 0) return false;

        var stack = new Stack<char>(s.Length);

        foreach (char c in s)
        {
            switch (c)
            {
                case '(':
                    stack.Push(')');
                    break;
                case '[':
                    stack.Push(']');
                    break;
                case '{':
                    stack.Push('}');
                    break;
                default:
                    if (stack.Count == 0 || stack.Pop() != c)
                    {
                        return false;
                    }
                    break;
            }
        }

        return stack.Count == 0;
    }
}

/// <summary>
/// LeetCode #155: Min Stack
/// Stack supporting Push, Pop, Top, and retrieving the minimum element in O(1) time.
/// </summary>
public sealed class MinStack
{
    private readonly Stack<(int Value, int CurrentMin)> _stack = new();

    public void Push(int val)
    {
        int min = _stack.Count == 0 ? val : Math.Min(val, _stack.Peek().CurrentMin);
        _stack.Push((val, min));
    }

    public void Pop()
    {
        if (_stack.Count == 0)
        {
            throw new InvalidOperationException("Stack is empty.");
        }
        _stack.Pop();
    }

    public int Top()
    {
        if (_stack.Count == 0)
        {
            throw new InvalidOperationException("Stack is empty.");
        }
        return _stack.Peek().Value;
    }

    public int GetMin()
    {
        if (_stack.Count == 0)
        {
            throw new InvalidOperationException("Stack is empty.");
        }
        return _stack.Peek().CurrentMin;
    }

    public int Count => _stack.Count;
}
