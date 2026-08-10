using HashCompare.Core;
using Xunit;

namespace HashCompare.Core.Tests;

public sealed class WildcardMatcherTests
{
    [Fact]
    public void CompileNoPatternsReturnsEmptyList()
    {
        var patterns = WildcardMatcher.Compile("");
        Assert.Empty(patterns);
    }

    [Fact]
    public void CompileSinglePatternCompilesCorrectly()
    {
        var patterns = WildcardMatcher.Compile("*.txt");
        Assert.Single(patterns);
        Assert.True(WildcardMatcher.IsMatch(patterns, "test.txt"));
        Assert.False(WildcardMatcher.IsMatch(patterns, "test.pdf"));
    }

    [Fact]
    public void CompileMultiplePatternsReturnsMultipleRegexes()
    {
        var patterns = WildcardMatcher.Compile("*.txt;*.log");
        Assert.Equal(2, patterns.Count);
        Assert.True(WildcardMatcher.IsMatch(patterns, "file.txt"));
        Assert.True(WildcardMatcher.IsMatch(patterns, "file.log"));
    }

    [Fact]
    public void IsMatchWithNoPatternsReturnsFalse()
    {
        var patterns = WildcardMatcher.Compile("");
        Assert.False(WildcardMatcher.IsMatch(patterns, "anyfile.txt"));
    }

    [Theory]
    [InlineData("*.txt", "file.txt", true)]
    [InlineData("*.txt", "folder/file.txt", true)]
    [InlineData("*.txt", "file.log", false)]
    [InlineData("test*", "test", true)]
    [InlineData("test*", "testing", true)]
    [InlineData("test*", "stest", false)]
    [InlineData("file?.txt", "file1.txt", true)]
    [InlineData("file?.txt", "file10.txt", false)]
    [InlineData("file?.txt", "file.txt", false)]
    public void IsMatchHonorsWildcards(string pattern, string value, bool expected)
    {
        var patterns = WildcardMatcher.Compile(pattern);
        Assert.Equal(expected, WildcardMatcher.IsMatch(patterns, value));
    }

    [Fact]
    public void SemicolonListMatchesAnyEntry()
    {
        var patterns = WildcardMatcher.Compile("bin; obj ;*.tmp");
        Assert.True(WildcardMatcher.IsMatch(patterns, "bin"));
        Assert.True(WildcardMatcher.IsMatch(patterns, "obj"));
        Assert.True(WildcardMatcher.IsMatch(patterns, "x.tmp"));
        Assert.False(WildcardMatcher.IsMatch(patterns, "src"));
    }

    [Theory]
    [InlineData(".tmp", "*.tmp")]      // bare dotted extension
    [InlineData("tmp", "*.tmp")]       // bare extension without dot
    [InlineData("*.tmp", "*.tmp")]     // already a wildcard: unchanged
    [InlineData("thumbs.db?", "thumbs.db?")]
    [InlineData(".tmp;log", "*.tmp;*.log")]
    [InlineData("", "")]
    public void ExpandFileExcludesNormalisesEntries(string raw, string expected)
    {
        Assert.Equal(expected, WildcardMatcher.ExpandFileExcludes(raw));
    }
}