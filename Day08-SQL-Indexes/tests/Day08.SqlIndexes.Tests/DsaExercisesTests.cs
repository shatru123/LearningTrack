using Xunit;
using Day08.SqlIndexes;

namespace Day08.SqlIndexes.Tests;

public class DsaExercisesTests
{
    [Theory]
    [InlineData("abcabcbb", 3)] // "abc"
    [InlineData("bbbbb", 1)]    // "b"
    [InlineData("pwwkew", 3)]   // "wke"
    [InlineData("", 0)]
    [InlineData(" ", 1)]
    [InlineData("au", 2)]
    public void LengthOfLongestSubstring_ComputesExpectedLength(string s, int expected)
    {
        int result = DsaExercises.LengthOfLongestSubstring(s);
        Assert.Equal(expected, result);
    }
}
