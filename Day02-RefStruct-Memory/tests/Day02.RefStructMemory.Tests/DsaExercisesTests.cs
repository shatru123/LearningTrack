using Xunit;
using Day02.RefStructMemory;

namespace Day02.RefStructMemory.Tests;

public class DsaExercisesTests
{
    [Theory]
    [InlineData("anagram", "nagaram", true)]
    [InlineData("rat", "car", false)]
    [InlineData("listen", "silent", true)]
    [InlineData("a", "ab", false)]
    public void IsAnagram_EvaluatesCorrectly(string s, string t, bool expected)
    {
        bool result = DsaExercises.IsAnagram(s, t);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GroupAnagrams_GroupsValidSets()
    {
        string[] input = { "eat", "tea", "tan", "ate", "nat", "bat" };
        var grouped = DsaExercises.GroupAnagrams(input);

        Assert.Equal(3, grouped.Count);

        // Verify "bat" is by itself
        Assert.Contains(grouped, g => g.Count == 1 && g.Contains("bat"));

        // Verify "nat" and "tan" are together
        Assert.Contains(grouped, g => g.Count == 2 && g.Contains("nat") && g.Contains("tan"));

        // Verify "eat", "tea", "ate" are together
        Assert.Contains(grouped, g => g.Count == 3 && g.Contains("eat") && g.Contains("tea") && g.Contains("ate"));
    }
}
