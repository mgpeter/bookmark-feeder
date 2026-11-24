using System.Text.RegularExpressions;

namespace BookmarkFeeder.WebApi.Services;

/// <summary>
/// The words of a search box query, read for substring matching: "moonlig" must find
/// "Moonlight", which full-text search cannot do because it only matches whole (stemmed) words.
/// </summary>
/// <remarks>
/// Reads the same websearch syntax the full-text branch accepts, and the web client's highlighter
/// uses the same reading: a "quoted phrase" is one fragment, a -word must be absent, and OR/AND
/// are operators rather than words. OR is not honoured here: every fragment must be present.
/// That only narrows this branch. Search unions it with the full-text match, which does honour OR.
/// </remarks>
public sealed partial record SearchFragments(IReadOnlyList<string> Include, IReadOnlyList<string> Exclude)
{
    /// <summary>Bounds the SQL: each fragment adds three ILIKE conditions.</summary>
    public const int MaxFragments = 8;

    [GeneratedRegex("\"([^\"]+)\"|(\\S+)")]
    private static partial Regex Tokens();

    public static SearchFragments Parse(string term)
    {
        var include = new List<string>();
        var exclude = new List<string>();

        foreach (Match token in Tokens().Matches(term))
        {
            if (token.Groups[1].Success)
            {
                Add(include, token.Groups[1].Value.Trim());
                continue;
            }

            var bare = token.Groups[2].Value;
            if (bare.Equals("or", StringComparison.OrdinalIgnoreCase) ||
                bare.Equals("and", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (bare.StartsWith('-'))
            {
                Add(exclude, bare[1..]);
            }
            else
            {
                // An unclosed quote reaches here as a bare token; the quote is not part of the word.
                Add(include, bare.Trim('"'));
            }
        }

        return new SearchFragments(include, exclude);
    }

    private static void Add(List<string> list, string fragment)
    {
        if (fragment.Length > 0 && list.Count < MaxFragments) list.Add(fragment);
    }
}
