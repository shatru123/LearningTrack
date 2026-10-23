using Xunit;

namespace Day41.DockerDotNet.Tests;

public class Day41Tests
{
    [Fact]
    public void ContainerGc_Calculates75PercentHeapLimit_LeavingHeadroomForNativeAndOS()
    {
        // 512 MB container limit
        long limitBytes = 512L * 1024 * 1024;
        var profile = ContainerMemoryCalculator.ComputeGcProfile(limitBytes, heapLimitPercent: 75, serverGc: true, allocatedCores: 2);

        // 75% of 512MB = 384MB
        long expectedHeap = 384L * 1024 * 1024;
        long expectedBuffer = 128L * 1024 * 1024;

        Assert.Equal(expectedHeap, profile.CalculatedHeapLimitBytes);
        Assert.Equal(expectedBuffer, profile.NativeAndOsBufferBytes);
        Assert.True(profile.IsServerGcEnabled);
        Assert.Equal(2, profile.ExpectedGcThreads);
    }

    [Fact]
    public void ContainerGc_ServerGcThreadCount_ScalesWithCpuCores()
    {
        long limitBytes = 1024L * 1024 * 1024;
        var profile = ContainerMemoryCalculator.ComputeGcProfile(limitBytes, heapLimitPercent: 80, serverGc: true, allocatedCores: 8);

        Assert.Equal(8, profile.ExpectedGcThreads);
    }

    [Fact]
    public void ContainerGc_WorkstationGc_UsesSingleThread()
    {
        long limitBytes = 1024L * 1024 * 1024;
        var profile = ContainerMemoryCalculator.ComputeGcProfile(limitBytes, heapLimitPercent: 80, serverGc: false, allocatedCores: 8);

        Assert.Equal(1, profile.ExpectedGcThreads);
        Assert.False(profile.IsServerGcEnabled);
    }

    [Fact]
    public void DockerfileValidator_AuditsProductionDockerfile_PassesAllChecks()
    {
        string dockerfile = """
        FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
        WORKDIR /src
        COPY Day41.DockerDotNet.csproj .
        RUN dotnet restore Day41.DockerDotNet.csproj
        COPY . .
        RUN dotnet build Day41.DockerDotNet.csproj -c Release --no-restore

        FROM build AS publish
        RUN dotnet publish Day41.DockerDotNet.csproj -c Release -o /app/publish

        FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS final
        WORKDIR /app
        USER app
        COPY --from=publish /app/publish .
        ENTRYPOINT ["dotnet", "Day41.DockerDotNet.dll"]
        """;

        var audit = DockerfileLintValidator.Audit(dockerfile);

        Assert.True(audit.HasMultiStageBuild);
        Assert.True(audit.HasLayerCachingOptimization);
        Assert.True(audit.UsesNonRootUser);
        Assert.True(audit.UsesChiseledOrMinimalRuntime);
        Assert.Empty(audit.Recommendations);
    }

    [Fact]
    public void DockerfileValidator_FlagsSingleStageAndRootUser_WhenMissing()
    {
        string badDockerfile = """
        FROM mcr.microsoft.com/dotnet/sdk:8.0
        WORKDIR /app
        COPY . .
        RUN dotnet run
        """;

        var audit = DockerfileLintValidator.Audit(badDockerfile);

        Assert.False(audit.HasMultiStageBuild);
        Assert.False(audit.HasLayerCachingOptimization);
        Assert.False(audit.UsesNonRootUser);
        Assert.False(audit.UsesChiseledOrMinimalRuntime);
        Assert.NotEmpty(audit.Recommendations);
    }

    [Fact]
    public void ClimbingStairs_BaseCases_ReturnsExpectedWays()
    {
        Assert.Equal(1, ClimbingStairsSolver.ClimbStairs(1));
        Assert.Equal(2, ClimbingStairsSolver.ClimbStairs(2));
    }

    [Fact]
    public void ClimbingStairs_LargerValues_MatchesFibonacciRecurrence()
    {
        // n=3: (1+1+1), (1+2), (2+1) -> 3
        Assert.Equal(3, ClimbingStairsSolver.ClimbStairs(3));
        // n=4: 5
        Assert.Equal(5, ClimbingStairsSolver.ClimbStairs(4));
        // n=5: 8
        Assert.Equal(8, ClimbingStairsSolver.ClimbStairs(5));
    }

    [Fact]
    public void MinCostClimbingStairs_Example1_ReturnsMinimumCost()
    {
        int[] cost = [10, 15, 20];
        // Start at index 1 (cost 15) and jump 2 steps to reach the top -> min cost 15
        int result = ClimbingStairsSolver.MinCostClimbingStairs(cost);
        Assert.Equal(15, result);
    }

    [Fact]
    public void MinCostClimbingStairs_Example2_ReturnsMinimumCost()
    {
        int[] cost = [1, 100, 1, 1, 1, 100, 1, 1, 100, 1];
        int result = ClimbingStairsSolver.MinCostClimbingStairs(cost);
        Assert.Equal(6, result);
    }

    [Fact]
    public void MinCostClimbingStairs_TwoSteps_ReturnsMinimumOfBoth()
    {
        int[] cost = [10, 20];
        int result = ClimbingStairsSolver.MinCostClimbingStairs(cost);
        Assert.Equal(10, result);
    }
}
