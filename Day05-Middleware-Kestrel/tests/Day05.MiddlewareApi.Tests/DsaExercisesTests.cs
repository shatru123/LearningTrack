using Xunit;
using Day05.MiddlewareApi;

namespace Day05.MiddlewareApi.Tests;

public class DsaExercisesTests
{
    [Theory]
    [InlineData("A man, a plan, a canal: Panama", true)]
    [InlineData("race a car", false)]
    [InlineData(" ", true)]
    [InlineData("0P", false)]
    public void IsPalindrome_EvaluatesCorrectly(string input, bool expected)
    {
        bool result = DsaExercises.IsPalindrome(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(new[] { 2, 7, 11, 15 }, 9, new[] { 1, 2 })]
    [InlineData(new[] { 2, 3, 4 }, 6, new[] { 1, 3 })]
    [InlineData(new[] { -1, 0 }, -1, new[] { 1, 2 })]
    public void TwoSum_Returns1BasedIndices(int[] numbers, int target, int[] expected)
    {
        int[] result = DsaExercises.TwoSum(numbers, target);
        Assert.Equal(expected, result);
    }
}
