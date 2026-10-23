using Xunit;

namespace Day35.ObservabilityStack.Tests;

public class Day35Tests
{
    [Fact]
    public void ObservabilityMetrics_RecordsRequestAndErrorCounts()
    {
        using var manager = new ObservabilityMetricsManager();

        manager.RecordRequest(durationMs: 45.2, isError: false, route: "/checkout", statusCode: 200);
        manager.RecordRequest(durationMs: 120.5, isError: true, route: "/checkout", statusCode: 500);
        manager.RecordRequest(durationMs: 15.0, isError: false, route: "/items", statusCode: 200);

        Assert.Equal(3, manager.TotalRequests);
        Assert.Equal(1, manager.TotalErrors);
        Assert.Equal(3, manager.ObservedDurations.Count);
    }

    [Fact]
    public void ObservabilityMetrics_Percentiles_CalculatesAccurateP50P90P95P99()
    {
        // 100 values from 1.0 to 100.0
        var latencies = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();

        var percentiles = ObservabilityMetricsManager.CalculatePercentiles(latencies);

        Assert.Equal(100, percentiles.TotalCount);
        Assert.Equal(50.5, percentiles.P50);
        Assert.Equal(90.1, percentiles.P90);
        Assert.Equal(95.05, percentiles.P95);
        Assert.Equal(99.01, percentiles.P99);
        Assert.Equal(50.5, percentiles.Mean);
    }

    [Fact]
    public void ObservabilityMetrics_PrometheusBuckets_GeneratesCumulativeCounts()
    {
        double[] latencies = [3.0, 8.0, 15.0, 40.0, 85.0, 200.0, 600.0, 1500.0];

        var buckets = ObservabilityMetricsManager.BuildPrometheusBuckets(latencies);

        // Standard boundaries: 5, 10, 25, 50, 100, 250, 500, 1000, +Inf
        var b5 = buckets.First(b => b.UpperBound == 5.0);
        var b10 = buckets.First(b => b.UpperBound == 10.0);
        var b25 = buckets.First(b => b.UpperBound == 25.0);
        var b100 = buckets.First(b => b.UpperBound == 100.0);
        var bInf = buckets.First(b => double.IsPositiveInfinity(b.UpperBound));

        Assert.Equal(1, b5.CumulativeCount); // 3.0
        Assert.Equal(2, b10.CumulativeCount); // 3.0, 8.0
        Assert.Equal(3, b25.CumulativeCount); // 3.0, 8.0, 15.0
        Assert.Equal(5, b100.CumulativeCount); // 3, 8, 15, 40, 85
        Assert.Equal(8, bInf.CumulativeCount); // all 8
    }

    [Fact]
    public void ObservabilityMetrics_StructuredLog_ContainsTraceAndSpanIds()
    {
        string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        string spanId = "00f067aa0ba902b7";
        string msg = "Payment processing started";

        string log = ObservabilityMetricsManager.FormatStructuredLog(msg, traceId, spanId, "Information");

        Assert.Contains(traceId, log);
        Assert.Contains(spanId, log);
        Assert.Contains("Information", log);
        Assert.Contains(msg, log);
    }

    [Fact]
    public void ObservabilityMetrics_EmptyLatencies_ReturnsZeroPercentiles()
    {
        var percentiles = ObservabilityMetricsManager.CalculatePercentiles([]);

        Assert.Equal(0, percentiles.TotalCount);
        Assert.Equal(0, percentiles.P50);
        Assert.Equal(0, percentiles.P99);
    }

    [Fact]
    public void Twitter_PostTweetAndGetNewsFeed_ReturnsOwnTweetsInDescendingOrder()
    {
        var twitter = new TwitterFeedSolver();
        twitter.PostTweet(userId: 1, tweetId: 101);
        twitter.PostTweet(userId: 1, tweetId: 102);
        twitter.PostTweet(userId: 1, tweetId: 103);

        var feed = twitter.GetNewsFeed(userId: 1);

        Assert.Equal([103, 102, 101], feed);
    }

    [Fact]
    public void Twitter_FollowAndGetNewsFeed_ReturnsCombinedTweetsOrderedByTimestamp()
    {
        var twitter = new TwitterFeedSolver();
        twitter.PostTweet(userId: 1, tweetId: 5);
        twitter.PostTweet(userId: 2, tweetId: 6);

        // User 1 follows User 2
        twitter.Follow(followerId: 1, followeeId: 2);

        var feed = twitter.GetNewsFeed(userId: 1);

        // Tweet 6 was posted after Tweet 5
        Assert.Equal([6, 5], feed);
    }

    [Fact]
    public void Twitter_Unfollow_StopsShowingUnfollowedTweets()
    {
        var twitter = new TwitterFeedSolver();
        twitter.PostTweet(userId: 1, tweetId: 11);
        twitter.PostTweet(userId: 2, tweetId: 22);

        twitter.Follow(1, 2);
        Assert.Equal([22, 11], twitter.GetNewsFeed(1));

        twitter.Unfollow(1, 2);
        Assert.Equal([11], twitter.GetNewsFeed(1));
    }

    [Fact]
    public void Twitter_GetNewsFeed_CapsAt10MostRecentTweets()
    {
        var twitter = new TwitterFeedSolver();

        // User posts 15 tweets
        for (int i = 1; i <= 15; i++)
        {
            twitter.PostTweet(userId: 42, tweetId: i);
        }

        var feed = twitter.GetNewsFeed(userId: 42);

        Assert.Equal(10, feed.Count);
        // Most recent should be 15 down to 6
        Assert.Equal([15, 14, 13, 12, 11, 10, 9, 8, 7, 6], feed);
    }

    [Fact]
    public void Twitter_SelfFollowAndUnfollow_DoesNotCorruptSelfTweetsInFeed()
    {
        var twitter = new TwitterFeedSolver();
        twitter.PostTweet(userId: 7, tweetId: 701);

        twitter.Follow(followerId: 7, followeeId: 7); // Self-follow attempt
        twitter.Unfollow(followerId: 7, followeeId: 7); // Self-unfollow attempt

        var feed = twitter.GetNewsFeed(userId: 7);

        Assert.Single(feed);
        Assert.Equal(701, feed[0]);
    }
}
