using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MaplesEcho.Services;

/// <summary>
/// The "transform" stage of the pipeline (receive → filter → extract speaker →
/// <b>transform</b> → store → render). Everything that turns raw Discord text
/// into a clean readable line lives here so v1.5 features (glossary, keyword
/// highlighting) slot in without restructuring. (Plan §5, §8b)
///
/// Pure and stateless: takes config-driven rules in, returns strings out. Easy
/// to unit-test without a game or a gateway.
/// </summary>
public static class MessageTransform
{
    // Custom emoji  <:name:12345>  /  <a:name:12345>  ->  :name:
    private static readonly Regex CustomEmoji =
        new(@"<a?:(\w+):\d+>", RegexOptions.Compiled);

    // User / role / channel mentions  <@123> <@!123> <@&123> <#123>
    private static readonly Regex Mention =
        new(@"<(@[!&]?|#)\d+>", RegexOptions.Compiled);

    // Markdown emphasis markers we strip to plain text: ** __ ~~ ` and stray *_
    private static readonly Regex Emphasis =
        new(@"(\*\*|__|~~|\*|_|`)", RegexOptions.Compiled);

    /// <summary>
    /// Strip the configurable prefix from a webhook author username to recover
    /// the speaker name. If the prefix doesn't match, fall back to the raw
    /// username rather than dropping the message. (Plan §5)
    /// </summary>
    public static string ExtractSpeaker(string authorUsername, string prefix)
    {
        if (string.IsNullOrEmpty(authorUsername))
            return "Unknown";

        if (!string.IsNullOrEmpty(prefix) &&
            authorUsername.StartsWith(prefix, StringComparison.Ordinal))
        {
            var stripped = authorUsername[prefix.Length..].Trim();
            return stripped.Length > 0 ? stripped : authorUsername;
        }

        return authorUsername;
    }

    /// <summary>
    /// Turn raw Discord markup into plain readable text: convert mentions and
    /// custom emoji, drop emphasis markers, collapse whitespace. (Plan §5)
    /// </summary>
    public static string CleanMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var s = content;
        s = CustomEmoji.Replace(s, ":$1:");
        s = Mention.Replace(s, string.Empty); // best-effort; raw ids add no value on screen
        s = Emphasis.Replace(s, string.Empty);
        s = s.Replace("\r\n", "\n").Replace('\r', '\n');
        return s.Trim();
    }

    /// <summary>Apply the user's find → replace glossary rules in order. (Plan §8b)</summary>
    public static string ApplyGlossary(string text, IReadOnlyList<GlossaryRule> rules)
    {
        if (rules is null || rules.Count == 0 || string.IsNullOrEmpty(text))
            return text;

        var s = text;
        foreach (var rule in rules)
        {
            if (!rule.Enabled || string.IsNullOrEmpty(rule.Find))
                continue;

            if (rule.IsRegex)
            {
                s = ApplyRegexRule(s, rule);
                continue;
            }

            var comparison = rule.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            s = ReplaceAll(s, rule.Find, rule.Replace ?? string.Empty, comparison, rule.WholeWord);
        }

        return s;
    }

    // Bounded so a pathological pattern can never stall the relay; on timeout
    // (or an invalid pattern) the rule is skipped and the text passes through.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static string ApplyRegexRule(string source, GlossaryRule rule)
    {
        try
        {
            var options = RegexOptions.CultureInvariant;
            if (!rule.CaseSensitive)
                options |= RegexOptions.IgnoreCase;
            return Regex.Replace(source, rule.Find, rule.Replace ?? string.Empty, options, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return source;
        }
        catch (RegexMatchTimeoutException)
        {
            return source;
        }
    }

    /// <summary>Full body transform: clean markdown, then apply the glossary.</summary>
    public static string TransformBody(string content, IReadOnlyList<GlossaryRule> glossary)
        => ApplyGlossary(CleanMarkdown(content), glossary);

    /// <summary>
    /// First enabled keyword rule that matches the text, or null. Rule order is
    /// priority order, so the most important keywords (her name) belong at the
    /// top of the list and decide the line's highlight color.
    /// </summary>
    public static KeywordRule? FirstKeywordMatch(string text, IReadOnlyList<KeywordRule> rules)
    {
        if (string.IsNullOrEmpty(text) || rules is null)
            return null;

        foreach (var rule in rules)
        {
            if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Word))
                continue;
            if (ContainsWord(text, rule.Word.Trim(), rule.WholeWord))
                return rule;
        }

        return null;
    }

    /// <summary>
    /// Case-insensitive keyword search. Whole-word mode requires a
    /// non-alphanumeric character (or the string edge) on both sides of the
    /// match, so "in"/"out" — THE FFXIV callouts — work without lighting up
    /// "point" or "shout". Word may be a phrase ("tank buster"); punctuation
    /// counts as a boundary, so "stack!" still matches "stack".
    /// </summary>
    public static bool ContainsWord(string text, string word, bool wholeWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
            return false;

        var index = 0;
        while (index <= text.Length - word.Length)
        {
            var next = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase);
            if (next < 0)
                return false;
            if (!wholeWord)
                return true;

            var beforeOk = next == 0 || !char.IsLetterOrDigit(text[next - 1]);
            var after = next + word.Length;
            var afterOk = after >= text.Length || !char.IsLetterOrDigit(text[after]);
            if (beforeOk && afterOk)
                return true;

            index = next + 1;
        }

        return false;
    }

    private static string ReplaceAll(string source, string find, string replace, StringComparison comparison, bool wholeWord = false)
    {
        if (string.IsNullOrEmpty(find))
            return source;

        var sb = new StringBuilder(source.Length);
        var index = 0;
        while (true)
        {
            var next = source.IndexOf(find, index, comparison);
            if (next < 0)
            {
                sb.Append(source, index, source.Length - index);
                break;
            }

            if (wholeWord)
            {
                var beforeOk = next == 0 || !char.IsLetterOrDigit(source[next - 1]);
                var after = next + find.Length;
                var afterOk = after >= source.Length || !char.IsLetterOrDigit(source[after]);
                if (!(beforeOk && afterOk))
                {
                    // Not a word boundary — keep the char at the match start and
                    // continue scanning from just past it.
                    sb.Append(source, index, next - index + 1);
                    index = next + 1;
                    continue;
                }
            }

            sb.Append(source, index, next - index);
            sb.Append(replace);
            index = next + find.Length;
        }

        return sb.ToString();
    }
}
