using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Parses comment blocks to extract metadata directives.
/// Supports both single-line (//) and block (/* */) comments.
/// </summary>
public class MixinCommentParser
{
    // Comment directive patterns
    private static readonly Regex ExportPattern = new(@"@export\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NamespacePattern = new(@"@namespace\s+(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SummaryPattern = new(@"@summary\s+(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParamPattern = new(@"@param\s+(\w+)\s+(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DefaultPattern = new(@"@default\s+(\w+)\s+(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GroupablePattern = new(@"@groupable\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GroupOptionsPattern = new(@"@groupoptions\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a comment block and extracts metadata.
    /// </summary>
    /// <param name="commentBlock">The comment text (can include // or /* */ markers).</param>
    /// <returns>The extracted metadata.</returns>
    public MixinMetadata ParseComments(string commentBlock)
    {
        var metadata = new MixinMetadata();

        if (string.IsNullOrWhiteSpace(commentBlock))
            return metadata;

        // Clean up comment markers
        var cleanedText = CleanCommentMarkers(commentBlock);

        // Check for @export
        metadata.IsExported = ExportPattern.IsMatch(cleanedText);

        // Extract @namespace
        var namespaceMatch = NamespacePattern.Match(cleanedText);
        if (namespaceMatch.Success)
        {
            metadata.Namespace = namespaceMatch.Groups[1].Value.Trim();
        }

        // Extract @summary
        var summaryMatch = SummaryPattern.Match(cleanedText);
        if (summaryMatch.Success)
        {
            metadata.Summary = summaryMatch.Groups[1].Value.Trim();
        }

        // Extract @param entries
        var paramMatches = ParamPattern.Matches(cleanedText);
        foreach (Match match in paramMatches)
        {
            var paramName = match.Groups[1].Value.Trim();
            var paramDesc = match.Groups[2].Value.Trim();
            metadata.ParamDescriptions[paramName] = paramDesc;
        }

        // Extract @default entries
        var defaultMatches = DefaultPattern.Matches(cleanedText);
        foreach (Match match in defaultMatches)
        {
            var paramName = match.Groups[1].Value.Trim();
            var defaultValue = match.Groups[2].Value.Trim();
            metadata.ParamDefaults[paramName] = defaultValue;
        }

        // Check for @groupable
        metadata.IsGroupable = GroupablePattern.IsMatch(cleanedText);

        // Extract @groupoptions
        var groupOptionsMatch = GroupOptionsPattern.Match(cleanedText);
        if (groupOptionsMatch.Success && int.TryParse(groupOptionsMatch.Groups[1].Value, out var options))
        {
            metadata.GroupOptions = options;
        }

        return metadata;
    }

    /// <summary>
    /// Removes comment markers and normalizes the text.
    /// </summary>
    private static string CleanCommentMarkers(string text)
    {
        // Remove block comment markers
        text = Regex.Replace(text, @"/\*+", "");
        text = Regex.Replace(text, @"\*+/", "");

        // Remove line comment markers and leading asterisks
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var cleanedLines = new List<string>();

        foreach (var line in lines)
        {
            var cleanedLine = line.Trim();

            // Remove // prefix
            if (cleanedLine.StartsWith("//"))
            {
                cleanedLine = cleanedLine.Substring(2).TrimStart();
            }
            // Remove leading * (from block comments)
            else if (cleanedLine.StartsWith("*"))
            {
                cleanedLine = cleanedLine.TrimStart('*').TrimStart();
            }

            if (!string.IsNullOrWhiteSpace(cleanedLine))
            {
                cleanedLines.Add(cleanedLine);
            }
        }

        return string.Join("\n", cleanedLines);
    }

    /// <summary>
    /// Extracts the comment block immediately preceding a function declaration.
    /// </summary>
    /// <param name="shaderContent">The full shader content.</param>
    /// <param name="functionStartIndex">The character index where the function declaration starts.</param>
    /// <returns>The comment block text, or empty string if none found.</returns>
    public string ExtractPrecedingComment(string shaderContent, int functionStartIndex)
    {
        if (functionStartIndex <= 0 || functionStartIndex >= shaderContent.Length)
            return string.Empty;

        // Work backwards from the function start to find comments
        var searchStart = functionStartIndex - 1;

        // Skip whitespace
        while (searchStart >= 0 && char.IsWhiteSpace(shaderContent[searchStart]))
        {
            searchStart--;
        }

        if (searchStart < 0)
            return string.Empty;

        // Check if we're at the end of a comment
        var comments = new List<string>();
        var currentPos = searchStart;

        while (currentPos >= 0)
        {
            // Skip whitespace
            while (currentPos >= 0 && char.IsWhiteSpace(shaderContent[currentPos]))
            {
                currentPos--;
            }

            if (currentPos < 0)
                break;

            // Check for end of line comment
            if (currentPos >= 1)
            {
                // Find the start of this line
                var lineStart = currentPos;
                while (lineStart > 0 && shaderContent[lineStart - 1] != '\n')
                {
                    lineStart--;
                }

                var line = shaderContent.Substring(lineStart, currentPos - lineStart + 1).Trim();

                if (line.StartsWith("//"))
                {
                    comments.Insert(0, line);
                    currentPos = lineStart - 1;
                    continue;
                }
            }

            // Check for block comment end
            if (currentPos >= 1 && shaderContent[currentPos - 1] == '*' && shaderContent[currentPos] == '/')
            {
                // Find the start of the block comment
                var blockStart = shaderContent.LastIndexOf("/*", currentPos - 1, StringComparison.Ordinal);
                if (blockStart >= 0)
                {
                    var blockComment = shaderContent.Substring(blockStart, currentPos - blockStart + 1);
                    comments.Insert(0, blockComment);
                    currentPos = blockStart - 1;
                    continue;
                }
            }

            // No more comments found
            break;
        }

        return string.Join("\n", comments);
    }
}
