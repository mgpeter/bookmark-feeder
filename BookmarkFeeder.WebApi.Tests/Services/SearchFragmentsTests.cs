using BookmarkFeeder.WebApi.Services;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Services;

public class SearchFragmentsTests
{
    [Fact]
    public void BareWords_AreEachAFragment()
    {
        var parsed = SearchFragments.Parse("moonlig  strea");

        parsed.Include.Should().Equal("moonlig", "strea");
        parsed.Exclude.Should().BeEmpty();
    }

    [Fact]
    public void AQuotedPhrase_IsOneFragment()
    {
        SearchFragments.Parse("\"game strea\" pc").Include.Should().Equal("game strea", "pc");
    }

    [Fact]
    public void NegatedWords_AreExcluded_AndOperatorsDropped()
    {
        var parsed = SearchFragments.Parse("alpha OR beta and -gamma -");

        parsed.Include.Should().Equal("alpha", "beta");
        parsed.Exclude.Should().Equal("gamma");
    }

    [Fact]
    public void AnUnclosedQuote_DoesNotBecomePartOfTheWord()
    {
        SearchFragments.Parse("\"unclosed").Include.Should().Equal("unclosed");
    }

    [Fact]
    public void TheNumberOfFragments_IsCapped()
    {
        var term = string.Join(' ', Enumerable.Range(0, 20).Select(i => $"w{i}"));

        SearchFragments.Parse(term).Include.Should().HaveCount(SearchFragments.MaxFragments);
    }
}
