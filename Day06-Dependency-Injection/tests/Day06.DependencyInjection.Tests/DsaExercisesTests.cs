using Xunit;
using Day06.DependencyInjection;

namespace Day06.DependencyInjection.Tests;

public class DsaExercisesTests
{
    [Fact]
    public void ThreeSum_FindsUniqueTriplets()
    {
        int[] nums = { -1, 0, 1, 2, -1, -4 };
        var triplets = DsaExercises.ThreeSum(nums);

        Assert.Equal(2, triplets.Count);

        Assert.Contains(triplets, t => t.SequenceEqual(new[] { -1, -1, 2 }));
        Assert.Contains(triplets, t => t.SequenceEqual(new[] { -1, 0, 1 }));
    }

    [Fact]
    public void ThreeSum_ReturnsEmpty_WhenNoTripletsExist()
    {
        int[] nums = { 0, 1, 1 };
        var triplets = DsaExercises.ThreeSum(nums);
        Assert.Empty(triplets);
    }

    [Theory]
    [InlineData(new[] { 1, 8, 6, 2, 5, 4, 8, 3, 7 }, 49)]
    [InlineData(new[] { 1, 1 }, 1)]
    [InlineData(new[] { 4, 3, 2, 1, 4 }, 16)]
    public void MaxArea_CalculatesMaxWaterContainer(int[] height, int expected)
    {
        int result = DsaExercises.MaxArea(height);
        Assert.Equal(expected, result);
    }
}
