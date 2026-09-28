using Xunit;
using Day04.GarbageCollection;

namespace Day04.GarbageCollection.Tests;

public class GcLaboratoryTests
{
    [Fact]
    public void GetRuntimeInfo_ReturnsValidConfiguration()
    {
        var info = GcLaboratory.GetRuntimeInfo();
        Assert.NotNull(info);
        Assert.True(info.MaxGeneration >= 2);
        Assert.True(info.TotalMemoryBytes > 0);
    }

    [Fact]
    public void GenerationalPromotion_PromotesFromGen0ThroughGen2()
    {
        var (initial, afterGen0, afterGen1) = GcLaboratory.DemonstrateGenerationalPromotion();

        Assert.Equal(0, initial);
        Assert.True(afterGen0 >= 1, $"Expected >= 1, but got {afterGen0}");
        Assert.True(afterGen1 >= 2, $"Expected 2, but got {afterGen1}");
    }

    [Fact]
    public void LohAllocation_AllocatesDirectlyInGen2()
    {
        var (smallGen, largeGen) = GcLaboratory.DemonstrateLohAllocation();

        Assert.Equal(0, smallGen);
        Assert.Equal(2, largeGen);
    }

    [Fact]
    public void CompareAllocationImpact_OptimizedGeneratesDramaticallyFewerBytes()
    {
        var result = GcLaboratory.CompareAllocationImpact(iterations: 50);

        Assert.True(result.NaiveConcatBytes > result.StringBuilderBytes,
            $"Naive ({result.NaiveConcatBytes}) should exceed StringBuilder ({result.StringBuilderBytes})");
        Assert.True(result.NaiveConcatBytes > result.OptimizedBytes,
            $"Naive ({result.NaiveConcatBytes}) should exceed Optimized ({result.OptimizedBytes})");
    }
}
