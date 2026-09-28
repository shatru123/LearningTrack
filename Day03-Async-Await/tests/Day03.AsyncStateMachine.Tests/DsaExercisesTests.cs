using Xunit;
using Day03.AsyncStateMachine;

namespace Day03.AsyncStateMachine.Tests;

public class DsaExercisesTests
{
    [Fact]
    public void TopKFrequent_ReturnsTopElements()
    {
        int[] nums = { 1, 1, 1, 2, 2, 3 };
        int k = 2;

        int[] result = DsaExercises.TopKFrequent(nums, k);

        Assert.Equal(2, result.Length);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
    }

    [Fact]
    public void TopKFrequent_HandlesSingleElement()
    {
        int[] nums = { 1 };
        int k = 1;

        int[] result = DsaExercises.TopKFrequent(nums, k);

        Assert.Equal(new[] { 1 }, result);
    }
}
