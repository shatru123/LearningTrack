# Deep Dive Notes: Video Streaming Architecture & Transcoding Pipelines

## 1. Adaptive Bitrate Streaming (ABR): HLS vs MPEG-DASH
- **HLS (HTTP Live Streaming)**:
  - Developed by Apple, industry standard for iOS, Safari, and widespread web players (video.js, hls.js).
  - Uses `.m3u8` playlist files (UTF-8 formatted text) and MPEG-2 TS (`.ts`) or fragmented MP4 (`.fmp4` / `.m4s`) media chunks.
  - Master playlist references child playlists; child playlists list discrete segments (typically 2 to 6 seconds each).
- **MPEG-DASH (Dynamic Adaptive Streaming over HTTP)**:
  - International standard (ISO/IEC 23009-1).
  - Uses an XML Media Presentation Description (`.mpd`) file.
  - Native in Android and Chrome.
- **Client-Side ABR Algorithm**:
  - The video player constantly monitors download throughput and playback buffer fullness.
  - If buffer is draining and download speed drops below 2.5Mbps, player requests subsequent chunks from the 480p playlist instead of 1080p, preventing playback stalls and buffering spinners.

## 2. Ingestion & Transcoding Infrastructure
- **Why Upload Direct to S3?**
  - Routing 50GB 4K video files through application API servers exhausts web server RAM, network sockets, and CPU.
  - Using presigned multipart URLs allows the client to stream parts directly to AWS S3 / Azure Blob Storage over HTTPS.
- **DAG Workflow Transcoding**:
  - Once upload finishes, S3 triggers an event to AWS SQS / Temporal orchestrator.
  - Distributed GPU workers (FFmpeg / AWS Elemental MediaConvert) download the raw file, split it into chunks, transcode simultaneously across dozens of compute nodes, assemble `.m3u8` playlists, and push assets to the origin bucket.

## 3. LeetCode #46 & #47: Permutations Pruning
- **Permutations I**:
  - Each state chooses an unused element: `for (int i = 0; i < nums.Length; i++) if (!used[i])`.
  - Recurse until `current.Count == nums.Length`. Total states: $N!$.
- **Permutations II**:
  - Why `!used[i - 1]`?
    - If `nums[i] == nums[i - 1]` and `used[i - 1]` is true, `nums[i - 1]` is an **ancestor** in the current permutation path, which is valid (e.g., both elements appear in the sequence).
    - If `used[i - 1]` is false, it means `nums[i - 1]` was previously chosen and **backtracked/completed** at the same depth. Picking `nums[i]` now would explore a duplicate subtree!
    - Therefore, `if (i > 0 && nums[i] == nums[i - 1] && !used[i - 1]) continue;` guarantees each unique value is picked exactly once at each permutation position.
