using Xunit;
using Day04.GarbageCollection;

namespace Day04.GarbageCollection.Tests;

public class DsaExercisesTests
{
    [Theory]
    [InlineData(new[] { 1, 2, 3, 4 }, new[] { 24, 12, 8, 6 })]
    [InlineData(new[] { -1, 1, 0, -3, 3 }, new[] { 0, 0, 9, 0, 0 })]
    [InlineData(new[] { 2, 3 }, new[] { 3, 2 })]
    public void ProductExceptSelf_ComputesCorrectProducts(int[] nums, int[] expected)
    {
        int[] result = DsaExercises.ProductExceptSelf(nums);
        Assert.Equal(expected, result);
    }
}
