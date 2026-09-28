using Xunit;
using Day07.MinimalApi;

namespace Day07.MinimalApi.Tests;

public class DsaExercisesTests
{
    [Theory]
    [InlineData(new[] { 7, 1, 5, 3, 6, 4 }, 5)] // Buy at 1, sell at 6 -> profit 5
    [InlineData(new[] { 7, 6, 4, 3, 1 }, 0)]    // Monotonically decreasing -> profit 0
    [InlineData(new[] { 2, 4, 1 }, 2)]          // Buy at 2, sell at 4 -> profit 2
    [InlineData(new[] { 1, 2 }, 1)]
    public void MaxProfit_CalculatesMaximumProfit(int[] prices, int expected)
    {
        int result = DsaExercises.MaxProfit(prices);
        Assert.Equal(expected, result);
    }
}
