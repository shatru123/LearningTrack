using Xunit;
using Day01.MemoryLab;

namespace Day01.MemoryLab.Tests;

public class DsaExercisesTests
{
    [Fact]
    public void TwoSum_FindsCorrectIndices()
    {
        int[] nums = { 2, 7, 11, 15 };
        int target = 9;

        int[] result = DsaExercises.TwoSum(nums, target);

        Assert.Equal(new[] { 0, 1 }, result);
    }

    [Fact]
    public void TwoSum_ThrowsWhenNoPairExists()
    {
        int[] nums = { 1, 2, 3 };
        Assert.Throws<InvalidOperationException>(() => DsaExercises.TwoSum(nums, 10));
    }

    [Theory]
    [InlineData(new int[] { 1, 2, 3, 1 }, true)]
    [InlineData(new int[] { 1, 2, 3, 4 }, false)]
    [InlineData(new int[] { 1, 1, 1, 3, 3, 4, 3, 2, 4, 2 }, true)]
    public void ContainsDuplicate_EvaluatesCorrectly(int[] nums, bool expected)
    {
        bool result = DsaExercises.ContainsDuplicate(nums);
        Assert.Equal(expected, result);
    }
}
