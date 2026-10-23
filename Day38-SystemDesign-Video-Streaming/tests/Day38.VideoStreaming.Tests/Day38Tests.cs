using Xunit;

namespace Day38.VideoStreaming.Tests;

public class Day38Tests
{
    [Fact]
    public void Hls_GenerateMasterPlaylist_ContainsAllRenditionBandwidthsAndResolutions()
    {
        string baseUrl = "https://cdn.videoplatform.com/vod/vid-101";
        string master = HlsPlaylistGenerator.GenerateMasterPlaylist(baseUrl);

        Assert.Contains("#EXTM3U", master);
        Assert.Contains("BANDWIDTH=5000000", master);
        Assert.Contains("RESOLUTION=1920x1080", master);
        Assert.Contains("1080p.m3u8", master);
        Assert.Contains("BANDWIDTH=600000", master);
        Assert.Contains("360p.m3u8", master);
    }

    [Fact]
    public void Hls_GenerateMediaPlaylist_ContainsTargetDurationAndSegments()
    {
        var segments = new List<VideoSegment>
        {
            new(0, 4.0, "segment_0.ts"),
            new(1, 4.0, "segment_1.ts"),
            new(2, 3.25, "segment_2.ts")
        };

        string playlist = HlsPlaylistGenerator.GenerateMediaPlaylist(4.0, segments);

        Assert.Contains("#EXT-X-TARGETDURATION:4", playlist);
        Assert.Contains("#EXTINF:4.000,", playlist);
        Assert.Contains("segment_0.ts", playlist);
        Assert.Contains("#EXTINF:3.250,", playlist);
        Assert.Contains("segment_2.ts", playlist);
        Assert.Contains("#EXT-X-ENDLIST", playlist);
    }

    [Fact]
    public void PresignedUpload_InitiateAndVerifyPart_SucceedsWithValidSignature()
    {
        var manager = new PresignedUploadManager("super-secret-hmac-key");
        var session = manager.InitiateUpload("vid-42", totalFileBytes: 15 * 1024 * 1024, partSizeBytes: 5 * 1024 * 1024);

        Assert.Equal(3, session.TotalParts);

        string presignedUrl = manager.GeneratePresignedPartUrl(session.UploadId, partNumber: 1, TimeSpan.FromMinutes(10));
        var queryParams = System.Web.HttpUtility.ParseQueryString(new Uri("https://dummy.com" + presignedUrl).Query);

        string uploadId = queryParams["uploadId"]!;
        int part = int.Parse(queryParams["part"]!);
        long exp = long.Parse(queryParams["exp"]!);
        string sig = queryParams["sig"]!;

        bool verified = manager.VerifyAndCompletePart(uploadId, part, exp, sig, "etag-chunk-1");
        Assert.True(verified);
        Assert.False(manager.IsUploadComplete(uploadId)); // only 1 of 3 complete
    }

    [Fact]
    public void PresignedUpload_TamperedSignature_FailsVerification()
    {
        var manager = new PresignedUploadManager("super-secret-hmac-key");
        var session = manager.InitiateUpload("vid-tamper", 1000);

        string presignedUrl = manager.GeneratePresignedPartUrl(session.UploadId, 1, TimeSpan.FromMinutes(10));
        var queryParams = System.Web.HttpUtility.ParseQueryString(new Uri("https://dummy.com" + presignedUrl).Query);

        long exp = long.Parse(queryParams["exp"]!);

        bool verified = manager.VerifyAndCompletePart(session.UploadId, 1, exp, "tampered-bad-signature", "etag");
        Assert.False(verified);
    }

    [Fact]
    public void PresignedUpload_ExpiredSignature_FailsVerification()
    {
        var manager = new PresignedUploadManager("super-secret-hmac-key");
        var session = manager.InitiateUpload("vid-exp", 1000);

        // Expired in past
        long pastExp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        bool verified = manager.VerifyAndCompletePart(session.UploadId, 1, pastExp, "sig", "etag");
        Assert.False(verified);
    }

    [Fact]
    public void PresignedUpload_FullCompletion_TracksAllParts()
    {
        var manager = new PresignedUploadManager("my-key");
        var session = manager.InitiateUpload("vid-all", totalFileBytes: 10 * 1024 * 1024, partSizeBytes: 5 * 1024 * 1024);

        Assert.Equal(2, session.TotalParts);

        for (int p = 1; p <= 2; p++)
        {
            var url = manager.GeneratePresignedPartUrl(session.UploadId, p, TimeSpan.FromMinutes(5));
            var q = System.Web.HttpUtility.ParseQueryString(new Uri("https://dummy.com" + url).Query);
            manager.VerifyAndCompletePart(session.UploadId, p, long.Parse(q["exp"]!), q["sig"]!, $"etag-{p}");
        }

        Assert.True(manager.IsUploadComplete(session.UploadId));
    }

    [Fact]
    public void CdnEdgeRules_PlaylistAndChunkCachingHeaders_SetsAppropriateTtls()
    {
        var playlistCache = CdnEdgeRules.GetCachePolicy("/vod/1080p.m3u8");
        var chunkCache = CdnEdgeRules.GetCachePolicy("/vod/segment_42.ts");

        Assert.Contains("must-revalidate", playlistCache.CacheControl);
        Assert.Equal(10, playlistCache.TtlSeconds);

        Assert.Contains("immutable", chunkCache.CacheControl);
        Assert.Equal(31536000, chunkCache.TtlSeconds);
    }

    [Fact]
    public void Permutations_DistinctElements_ReturnsFactorialCount()
    {
        int[] nums = [1, 2, 3];
        var perms = PermutationsSolver.Permute(nums);

        // 3! = 6
        Assert.Equal(6, perms.Count);
        Assert.Contains(perms, p => p.SequenceEqual([1, 2, 3]));
        Assert.Contains(perms, p => p.SequenceEqual([3, 2, 1]));
    }

    [Fact]
    public void PermutationsUnique_WithDuplicates_ReturnsUniqueCount()
    {
        int[] nums = [1, 1, 2];
        var perms = PermutationsSolver.PermuteUnique(nums);

        // 3! / 2! = 3 unique permutations: [1,1,2], [1,2,1], [2,1,1]
        Assert.Equal(3, perms.Count);
        Assert.Contains(perms, p => p.SequenceEqual([1, 1, 2]));
        Assert.Contains(perms, p => p.SequenceEqual([1, 2, 1]));
        Assert.Contains(perms, p => p.SequenceEqual([2, 1, 1]));
    }

    [Fact]
    public void PermutationsUnique_AllDuplicates_ReturnsSinglePermutation()
    {
        int[] nums = [5, 5, 5, 5];
        var perms = PermutationsSolver.PermuteUnique(nums);

        Assert.Single(perms);
        Assert.Equal([5, 5, 5, 5], perms[0]);
    }
}
