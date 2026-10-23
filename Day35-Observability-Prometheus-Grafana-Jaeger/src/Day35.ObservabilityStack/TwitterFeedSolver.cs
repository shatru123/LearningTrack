namespace Day35.ObservabilityStack;

public class TwitterFeedSolver
{
    private static int _globalTimestamp = 0;

    public record Tweet(int TweetId, int Timestamp);

    private readonly Dictionary<int, List<Tweet>> _userTweets = new();
    private readonly Dictionary<int, HashSet<int>> _following = new();

    public TwitterFeedSolver()
    {
    }

    /// <summary>
    /// Composes a new tweet with ID tweetId by the user userId.
    /// Time Complexity: O(1).
    /// </summary>
    public void PostTweet(int userId, int tweetId)
    {
        if (!_userTweets.ContainsKey(userId))
        {
            _userTweets[userId] = new List<Tweet>();
        }

        var timestamp = Interlocked.Increment(ref _globalTimestamp);
        _userTweets[userId].Add(new Tweet(tweetId, timestamp));
    }

    /// <summary>
    /// Retrieves the 10 most recent tweet IDs in the user's news feed (from self and followees).
    /// Ordered from most recent to least recent.
    /// Uses Max-Heap (k-way merge) over sorted tweet lists.
    /// Time Complexity: O(F * log F + 10 * log F) where F = number of followees, Space: O(F).
    /// </summary>
    public IList<int> GetNewsFeed(int userId)
    {
        var result = new List<int>(10);

        // Gather all users contributing to this user's feed: self + followed users
        var relevantUsers = new HashSet<int> { userId };
        if (_following.TryGetValue(userId, out var followees))
        {
            foreach (var f in followees)
            {
                relevantUsers.Add(f);
            }
        }

        // Max-heap of (Tweet, user, indexInUserList).
        // PriorityQueue in .NET is min-heap by default, so use -timestamp as priority for max-heap behavior.
        var maxHeap = new PriorityQueue<(Tweet Tweet, int UserId, int Index), int>();

        foreach (var u in relevantUsers)
        {
            if (_userTweets.TryGetValue(u, out var tweets) && tweets.Count > 0)
            {
                int lastIdx = tweets.Count - 1;
                var latestTweet = tweets[lastIdx];
                maxHeap.Enqueue((latestTweet, u, lastIdx), -latestTweet.Timestamp);
            }
        }

        while (maxHeap.Count > 0 && result.Count < 10)
        {
            var (tweet, u, idx) = maxHeap.Dequeue();
            result.Add(tweet.TweetId);

            int nextIdx = idx - 1;
            if (nextIdx >= 0)
            {
                var nextTweet = _userTweets[u][nextIdx];
                maxHeap.Enqueue((nextTweet, u, nextIdx), -nextTweet.Timestamp);
            }
        }

        return result;
    }

    /// <summary>
    /// Follower starts following followee.
    /// A user cannot follow themselves.
    /// </summary>
    public void Follow(int followerId, int followeeId)
    {
        if (followerId == followeeId) return;

        if (!_following.ContainsKey(followerId))
        {
            _following[followerId] = new HashSet<int>();
        }

        _following[followerId].Add(followeeId);
    }

    /// <summary>
    /// Follower unfollows followee.
    /// </summary>
    public void Unfollow(int followerId, int followeeId)
    {
        if (followerId == followeeId) return;

        if (_following.TryGetValue(followerId, out var followees))
        {
            followees.Remove(followeeId);
        }
    }
}
