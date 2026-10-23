namespace Day41.DockerDotNet;

public record ContainerGcConfiguration(
    long ContainerMemoryLimitBytes,
    int HeapHardLimitPercent,
    long CalculatedHeapLimitBytes,
    long NativeAndOsBufferBytes,
    bool IsServerGcEnabled,
    int ExpectedGcThreads
);

public static class ContainerMemoryCalculator
{
    /// <summary>
    /// Calculates .NET GC Heap limits and safe native/OS headroom from cgroup memory limits.
    /// </summary>
    public static ContainerGcConfiguration ComputeGcProfile(
        long containerMemoryLimitBytes,
        int heapLimitPercent = 75,
        bool serverGc = true,
        int allocatedCores = 2)
    {
        if (heapLimitPercent <= 0 || heapLimitPercent > 95)
            throw new ArgumentOutOfRangeException(nameof(heapLimitPercent), "Heap percent should be between 1 and 95");

        long heapLimit = (long)(containerMemoryLimitBytes * (heapLimitPercent / 100.0));
        long buffer = containerMemoryLimitBytes - heapLimit;
        int gcThreads = serverGc ? Math.Max(1, allocatedCores) : 1;

        return new ContainerGcConfiguration(
            ContainerMemoryLimitBytes: containerMemoryLimitBytes,
            HeapHardLimitPercent: heapLimitPercent,
            CalculatedHeapLimitBytes: heapLimit,
            NativeAndOsBufferBytes: buffer,
            IsServerGcEnabled: serverGc,
            ExpectedGcThreads: gcThreads
        );
    }
}

public record DockerfileAuditResult(
    bool HasMultiStageBuild,
    bool HasLayerCachingOptimization,
    bool UsesNonRootUser,
    bool UsesChiseledOrMinimalRuntime,
    List<string> Recommendations
);

public static class DockerfileLintValidator
{
    public static DockerfileAuditResult Audit(string dockerfileText)
    {
        var lines = dockerfileText.Split('\n', StringSplitOptions.TrimEntries);
        var recommendations = new List<string>();

        int fromCount = lines.Count(l => l.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase));
        bool multiStage = fromCount > 1;
        if (!multiStage) recommendations.Add("Use multi-stage builds to exclude SDK from runtime image");

        // Check restore before full copy
        int copyCsprojIdx = Array.FindIndex(lines, l => l.Contains("COPY", StringComparison.OrdinalIgnoreCase) && l.Contains(".csproj", StringComparison.OrdinalIgnoreCase));
        int restoreIdx = Array.FindIndex(lines, l => l.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase));
        int copyAllIdx = Array.FindIndex(lines, l => l.Contains("COPY . .", StringComparison.OrdinalIgnoreCase));

        bool layerCaching = copyCsprojIdx != -1 && restoreIdx != -1 && copyAllIdx != -1 &&
                            copyCsprojIdx < restoreIdx && restoreIdx < copyAllIdx;

        if (!layerCaching) recommendations.Add("Optimize layer caching: copy .csproj and run dotnet restore before copying full source");

        bool nonRoot = lines.Any(l => l.StartsWith("USER ", StringComparison.OrdinalIgnoreCase) && !l.Contains("root", StringComparison.OrdinalIgnoreCase));
        if (!nonRoot) recommendations.Add("Specify an unprivileged non-root user (e.g. USER app)");

        bool minimal = lines.Any(l => l.Contains("chiseled", StringComparison.OrdinalIgnoreCase) || l.Contains("alpine", StringComparison.OrdinalIgnoreCase));
        if (!minimal) recommendations.Add("Use chiseled or alpine base image to minimize attack surface");

        return new DockerfileAuditResult(
            HasMultiStageBuild: multiStage,
            HasLayerCachingOptimization: layerCaching,
            UsesNonRootUser: nonRoot,
            UsesChiseledOrMinimalRuntime: minimal,
            Recommendations: recommendations
        );
    }
}
