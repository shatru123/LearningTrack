# Day 38: System Design: Video Streaming System (YouTube/Netflix)

## Architectural Overview
A video streaming system (YouTube, Netflix) ingests raw media files, transcodes them into multiple resolutions/bitrates, splits them into short chunk segments, and delivers them globally via Adaptive Bitrate Streaming (ABR) and CDN edge networks.

This module implements key components of video streaming pipelines in .NET 8:
- **Adaptive Bitrate Streaming (ABR) & HLS Manifests**:
  - Master Playlist (`.m3u8`): Encodes multi-variant renditions (1080p @ 5Mbps, 720p @ 2.5Mbps, 480p @ 1.2Mbps, 360p @ 600kbps) with `#EXT-X-STREAM-INF`.
  - Media Variant Playlist (`.m3u8`): Specifies target chunk duration (`#EXT-X-TARGETDURATION:4`) and sequenced `.ts` segment media URLs.
- **Direct S3 / Blob Multipart Uploads with HMAC Presigned URLs**:
  - Bypasses web servers by allowing clients to upload multi-gigabyte video chunks directly to object storage.
  - Generates and verifies HMAC-SHA256 signatures with Unix timestamp expiration.
  - Multipart session tracking ensuring all chunks are uploaded and verified via ETag before transcoding begins.
- **CDN Edge Caching Strategy**:
  - Master/Media Playlists: Short TTL (`max-age=10, must-revalidate`) for rapid adaptation and live stream updates.
  - Video Segments (`.ts` / `.m4s`): Immutable assets with maximum TTL (`max-age=31536000, immutable`), maximizing edge cache hit ratios (>98%).

```
Client Video Upload (10GB)
            │
            ├─ 1. POST /api/upload/initiate (PresignedUploadManager)
            ├─ 2. Direct Multipart Upload (S3/Blob with HMAC presigned URLs)
            ▼
      Object Storage (S3 / GCS)
            │
            ▼ (S3 Event Notification)
┌───────────────────────────────────────┐
│     Transcoding Pipeline (FFmpeg)     │
│ Chunk into 4-sec segments             │
│ Transcode to 1080p, 720p, 480p, 360p  │
└───────────────────┬───────────────────┘
                    │
                    ▼
     Generate Master & Media .m3u8
                    │
                    ▼
┌───────────────────────────────────────┐
│          CDN Edge Cache               │
│ - Playlists: TTL 10s                  │
│ - Chunks (.ts): TTL 1 Year (Immutable)│
└───────────────────┬───────────────────┘
                    │
                    ▼
     Client Video Player (HLS / DASH)
     Dynamic resolution switching (ABR)
```

---

## LeetCode #46 & #47: Permutations I & II (Medium)
- **LeetCode #46 (Permutations)**:
  - Generate all $N!$ permutations of unique elements.
  - Backtracking with a boolean `used` array.
- **LeetCode #47 (Permutations II)**:
  - Generate unique permutations of an array containing duplicates.
  - Sorts array and applies the duplicate pruning condition:
    `if (i > 0 && nums[i] == nums[i - 1] && !used[i - 1]) continue;`
  - Guarantees $100\%$ unique permutations without post-processing set lookups.

---

## Running Tests
```bash
dotnet test Day38-SystemDesign-Video-Streaming/tests/Day38.VideoStreaming.Tests/Day38.VideoStreaming.Tests.csproj
```
10 unit tests validate master/variant HLS playlist generation, HMAC presigned URL generation/verification, expiry handling, CDN cache-control policies, and permutations backtracking.
