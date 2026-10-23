using System.Security.Cryptography;
using System.Text;

namespace Day38.VideoStreaming;

public enum VideoResolution
{
    Res360p,
    Res480p,
    Res720p,
    Res1080p
}

public record VideoRendition(VideoResolution Resolution, int Width, int Height, int BitrateBps, string PlaylistName);

public record VideoSegment(int Index, double DurationSeconds, string Uri);

public class HlsPlaylistGenerator
{
    public static readonly IReadOnlyList<VideoRendition> StandardRenditions = new List<VideoRendition>
    {
        new(VideoResolution.Res1080p, 1920, 1080, 5000000, "1080p.m3u8"),
        new(VideoResolution.Res720p, 1280, 720, 2500000, "720p.m3u8"),
        new(VideoResolution.Res480p, 854, 480, 1200000, "480p.m3u8"),
        new(VideoResolution.Res360p, 640, 360, 600000, "360p.m3u8")
    };

    /// <summary>
    /// Generates HLS Master Playlist referencing multiple ABR bitrate streams.
    /// </summary>
    public static string GenerateMasterPlaylist(string baseUrl, IEnumerable<VideoRendition>? renditions = null)
    {
        renditions ??= StandardRenditions;
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:4");

        foreach (var r in renditions.OrderByDescending(x => x.BitrateBps))
        {
            sb.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={r.BitrateBps},RESOLUTION={r.Width}x{r.Height},NAME=\"{r.Height}p\"");
            sb.AppendLine($"{baseUrl.TrimEnd('/')}/{r.PlaylistName}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates HLS Media Variant Playlist (.m3u8) referencing .ts or .m4s video chunks.
    /// </summary>
    public static string GenerateMediaPlaylist(double targetDuration, IEnumerable<VideoSegment> segments)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:4");
        sb.AppendLine($"#EXT-X-TARGETDURATION:{Math.Ceiling(targetDuration)}");
        sb.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        foreach (var seg in segments.OrderBy(s => s.Index))
        {
            sb.AppendLine($"#EXTINF:{seg.DurationSeconds:0.000},");
            sb.AppendLine(seg.Uri);
        }

        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }
}

public record MultipartUploadSession(
    string UploadId,
    string VideoId,
    int TotalParts,
    long PartSizeBytes,
    Dictionary<int, string> CompletedParts
);

public class PresignedUploadManager
{
    private readonly byte[] _hmacSecret;
    private readonly Dictionary<string, MultipartUploadSession> _activeSessions = new();
    private readonly object _lock = new();

    public PresignedUploadManager(string secretKey)
    {
        _hmacSecret = Encoding.UTF8.GetBytes(secretKey);
    }

    public MultipartUploadSession InitiateUpload(string videoId, long totalFileBytes, long partSizeBytes = 5 * 1024 * 1024)
    {
        int totalParts = (int)Math.Ceiling((double)totalFileBytes / partSizeBytes);
        var session = new MultipartUploadSession(
            UploadId: Guid.NewGuid().ToString("N"),
            VideoId: videoId,
            TotalParts: totalParts,
            PartSizeBytes: partSizeBytes,
            CompletedParts: new Dictionary<int, string>()
        );

        lock (_lock)
        {
            _activeSessions[session.UploadId] = session;
        }

        return session;
    }

    public string GeneratePresignedPartUrl(string uploadId, int partNumber, TimeSpan validDuration)
    {
        long expiry = DateTimeOffset.UtcNow.Add(validDuration).ToUnixTimeSeconds();
        string payload = $"{uploadId}:{partNumber}:{expiry}";
        using var hmac = new HMACSHA256(_hmacSecret);
        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        string signature = Convert.ToHexString(sigBytes).ToLowerInvariant();

        return $"/storage/upload?uploadId={uploadId}&part={partNumber}&exp={expiry}&sig={signature}";
    }

    public bool VerifyAndCompletePart(string uploadId, int partNumber, long expiry, string signature, string partEtag)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
        {
            return false; // Expired
        }

        string payload = $"{uploadId}:{partNumber}:{expiry}";
        using var hmac = new HMACSHA256(_hmacSecret);
        var expectedSig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        if (expectedSig != signature)
        {
            return false; // Tampered signature
        }

        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(uploadId, out var session))
            {
                return false;
            }

            session.CompletedParts[partNumber] = partEtag;
            return true;
        }
    }

    public bool IsUploadComplete(string uploadId)
    {
        lock (_lock)
        {
            if (_activeSessions.TryGetValue(uploadId, out var session))
            {
                return session.CompletedParts.Count == session.TotalParts;
            }
            return false;
        }
    }
}

public record CdnCacheHeaders(string CacheControl, int TtlSeconds);

public static class CdnEdgeRules
{
    public static CdnCacheHeaders GetCachePolicy(string resourcePath)
    {
        if (resourcePath.EndsWith(".m3u8"))
        {
            // Master/Media playlists are dynamic and short-lived
            return new CdnCacheHeaders("public, max-age=10, must-revalidate", 10);
        }
        else if (resourcePath.EndsWith(".ts") || resourcePath.EndsWith(".m4s"))
        {
            // Immutable video chunk segments - cache aggressively for 1 year
            return new CdnCacheHeaders("public, max-age=31536000, immutable", 31536000);
        }

        return new CdnCacheHeaders("public, max-age=3600", 3600);
    }
}
