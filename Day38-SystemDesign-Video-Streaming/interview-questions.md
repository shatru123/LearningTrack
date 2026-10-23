# Day 38: Senior & Staff Interview Questions & Answers

### Q1: How does Adaptive Bitrate Streaming (ABR) work in modern video architectures (YouTube, Netflix)?
**Answer**:
ABR allows video playback to dynamically adapt to varying client network conditions and device capabilities in real time.
1. During transcoding, the raw video is encoded into multiple bitrates/resolutions (e.g. 1080p, 720p, 480p, 360p) and sliced into short segments (typically 2 to 6 seconds each).
2. The client video player downloads a Master Playlist (`.m3u8` or `.mpd`) detailing available bitrates.
3. As the player downloads chunks, it monitors the download throughput and client playback buffer level (e.g. 15 seconds of buffered video).
4. If network bandwidth degrades (e.g., mobile user entering an elevator), the player automatically requests the next 4-second chunk from a lower bitrate playlist (e.g. 480p instead of 1080p) without interrupting audio/video playback or causing buffering spinners.

---

### Q2: What is the purpose of HLS Master vs Media playlists, and how are they structured?
**Answer**:
- **Master Playlist (`master.m3u8`)**:
  - Serves as the index of all available renditions.
  - Lists `#EXT-X-STREAM-INF` tags specifying `BANDWIDTH`, `RESOLUTION`, `CODECS`, and audio tracks, each pointing to a Variant Media Playlist URL.
- **Media Playlist (`1080p.m3u8`)**:
  - Contains the actual sequence of video chunk segments for a specific resolution.
  - Specifies `#EXT-X-TARGETDURATION`, sequence numbers, and individual `#EXTINF` tags with chunk durations followed by the `.ts` or `.m4s` segment URLs.

---

### Q3: Why should client video uploads never pass through web application servers, and how do presigned multipart uploads solve this?
**Answer**:
Routing massive video files (e.g. 10GB–50GB) through web application servers:
- Exhausts server RAM, saturates network interfaces, and ties up web server worker threads for minutes or hours.
- If the network drops at 99%, the entire file upload fails.
- **Presigned Multipart Upload Solution**:
  1. The client requests an upload session from the API. The API contacts S3/GCS to initiate a multipart upload and returns signed upload URLs for each 5MB–10MB part.
  2. The client streams each part directly to the object storage bucket over HTTPS.
  3. Parts can be uploaded in parallel, and failed parts are retried individually without re-uploading the entire file.
  4. Once all parts are finished, the client notifies the API, which instructs S3 to assemble the completed object.

---

### Q4: How do you design CDN caching strategies for video streaming to achieve >98% cache hit ratio?
**Answer**:
Segment video assets by mutability:
1. **Video Chunks (`.ts`, `.m4s`)**:
   - Chunks are content-addressed and **immutable**. Chunk #42 of 1080p will never change.
   - Set HTTP headers: `Cache-Control: public, max-age=31536000, immutable`.
   - CDN edge nodes cache chunks indefinitely. Once cached at an edge POP, subsequent viewers in that geographic region fetch chunks from local edge NVMe drives rather than traversing back to the origin.
2. **Playlists (`.m3u8`)**:
   - Master playlists are cached for short periods (e.g., `max-age=60` for VOD; `max-age=2` for Live streams).
   - Live stream playlists append new chunks as the stream progresses, so edge caches must revalidate frequently.

---

### Q5: How do you protect premium video content from unauthorized downloads and hotlinking (DRM & Signed URLs)?
**Answer**:
1. **Signed URLs / Cookies**:
   - Issue HMAC-signed tokens with expiration timestamps and client IP restrictions.
   - CDN edge nodes validate the signature before serving playlist or chunk files; unauthorized requests receive HTTP 403 Forbidden without touching origin.
2. **Digital Rights Management (DRM)**:
   - Encrypt video chunks using AES-128 (Common Encryption `cenc`).
   - Integrated with platform DRM licensing systems: Apple FairPlay (iOS/Safari), Google Widevine (Android/Chrome), Microsoft PlayReady (Windows/Edge).
   - The player requests an encrypted license key from a DRM Key Server via an authenticated API token. Without the hardware-backed key, the raw `.ts` files cannot be decoded.

---

### Q6: How does a distributed transcoding pipeline scale to handle thousands of concurrent video uploads?
**Answer**:
1. An S3 bucket notification emits an `ObjectCreated` event to an AWS SQS queue or Apache Kafka topic.
2. A workflow orchestrator (e.g., Temporal or AWS Step Functions) partitions the video into chunks or keyframe (GOP) boundaries.
3. GPU worker nodes (auto-scaled on spot instances) claim individual transcoding jobs:
   - Node 1 encodes chunk 1–10 into 1080p H.264/AV1.
   - Node 2 encodes chunk 1–10 into 720p.
   - Node 3 encodes chunk 11–20 into 1080p.
4. An aggregation worker stitches the manifests, generates thumbnails, and updates the database record to `Status = "Published"`.

---

### Q7: What are the differences between H.264 (AVC), H.265 (HEVC), and AV1 codecs?
**Answer**:
- **H.264 / AVC**: Universal compatibility across 99.9% of devices, browsers, and legacy hardware. Moderate compression efficiency.
- **H.265 / HEVC**: ~50% bitrate reduction compared to H.264 at identical visual quality. Standard for 4K/HDR; however, burdened by complex patent licensing pools, limiting native web browser support (predominantly Apple and Smart TVs).
- **AV1**: Open-source, royalty-free codec developed by the Alliance for Open Media (Google, Netflix, Apple, Amazon). ~30% more efficient than HEVC, but requires high computational power to encode. Netflix and YouTube encode high-traffic videos in AV1 for massive bandwidth savings.

---

### Q8: In LeetCode #46 (Permutations), what is the time complexity and recursion tree structure?
**Answer**:
- For an array of size $N$, there are $N!$ leaf nodes in the recursion tree.
- At the root level, there are $N$ choices; at depth 1, there are $N - 1$ choices, ..., down to 1 choice at depth $N$.
- The number of operations to copy each permutation of length $N$ into the output list is $O(N)$.
- Total Time Complexity: $O(N \times N!)$.
- Space Complexity: $O(N)$ auxiliary space for the recursion stack and `used` boolean array.

---

### Q9: In LeetCode #47 (Permutations II), why does `!used[i - 1]` correctly identify duplicate branches, and what would happen if `used[i - 1]` were checked instead?
**Answer**:
- In a sorted array with duplicates (e.g., `[1, 1, 2]`):
- If `nums[i] == nums[i - 1]` and `!used[i - 1]`:
  - It means `nums[i - 1]` was already chosen at the current recursion level, explored completely, and subsequently unmarked (`used[i - 1] = false`) during backtracking.
  - Selecting `nums[i]` now would initiate an identical recursion subtree. Skipping when `!used[i - 1]` ensures only the first duplicate element is picked as the head of a branch at any level.
- If one checked `used[i - 1]` instead, it would prune elements that belong to the *same* permutation path (ancestor-descendant relationship), preventing valid duplicate occurrences (like `[1, 1, 2]`) from ever being formed.

---

### Q10: How do you calculate video storage and bandwidth costs for a platform streaming 1 million hours of video daily?
**Answer**:
1. **Bandwidth Estimation**:
   - 1M hours = 3,600,000,000 seconds.
   - Assume average 1080p/720p ABR bitrate of 3 Mbps ($3 \times 10^6 \text{ bits/sec} = 0.375 \text{ MB/sec}$).
   - Daily data transfer: $3.6 \times 10^9 \text{ sec} \times 0.375 \text{ MB} = 1.35 \times 10^9 \text{ MB} = 1.35 \text{ PB/day}$.
   - At CDN negotiated volume rates (\$0.01 per GB), egress cost $\approx \$13,500\text{/day}$.
2. **Storage Estimation**:
   - 1 hour of video transcoded across 4 resolutions $\approx 2.5 \text{ GB}$.
   - If 10,000 hours uploaded daily: $10,000 \times 2.5 \text{ GB} = 25 \text{ TB/day}$.
   - Stored on S3 Standard / Infrequent Access with lifecycle tiering to Glacier after 90 days.
