// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace XmlSyntax.Models;

public static class StringExtensions
{
    /// <summary>
    /// Returns a copy of <paramref name="text"/> with the given character ranges removed.
    /// Overlapping/adjacent ranges are merged before splicing. All <see cref="Range"/> bounds
    /// are interpreted as offsets from the start of the string (no <see cref="Index.IsFromEnd"/> support).
    /// </summary>
    public static string RemoveRanges(this string text, IEnumerable<Range> ranges)
    {
        if (text is null || ranges is null) return text;

        var merged = MergeRanges(ranges, text.Length);
        if (merged.Count == 0) return text;

        var removed = 0;
        for (var i = 0; i < merged.Count; i++) removed += merged[i].End.Value - merged[i].Start.Value;

        var builder = new StringBuilder(text.Length - removed);
        var cursor = 0;
        foreach (var range in merged)
        {
            var start = range.Start.Value;
            if (start > cursor) builder.Append(text, cursor, start - cursor);
            cursor = range.End.Value;
        }
        if (cursor < text.Length) builder.Append(text, cursor, text.Length - cursor);

        return builder.ToString();
    }

    private static List<Range> MergeRanges(IEnumerable<Range> ranges, int textLength)
    {
        var normalized = new List<Range>();
        foreach (var r in ranges)
        {
            if (r.Start.IsFromEnd || r.End.IsFromEnd)
            {
                throw new ArgumentException("Ranges with from-end indices are not supported.", nameof(ranges));
            }

            var start = Math.Max(0, Math.Min(r.Start.Value, textLength));
            var end = Math.Max(start, Math.Min(r.End.Value, textLength));
            if (end > start) normalized.Add(new Range(start, end));
        }

        if (normalized.Count <= 1) return normalized;

        normalized.Sort(static (a, b) =>
        {
            var c = a.Start.Value.CompareTo(b.Start.Value);
            return c != 0 ? c : a.End.Value.CompareTo(b.End.Value);
        });

        var merged = new List<Range>(normalized.Count);
        var current = normalized[0];
        for (var i = 1; i < normalized.Count; i++)
        {
            var next = normalized[i];
            if (next.Start.Value <= current.End.Value)
            {
                if (next.End.Value > current.End.Value) current = new Range(current.Start.Value, next.End.Value);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);
        return merged;
    }

    /// <summary>
    /// Given the provided string, line, and column, returns the absoluted index for the provided location. Returns -1 if out of bounds.
    /// </summary>
    /// <param name="">Text context</param>
    /// <param name="line">Line Number (1-index)</param>
    /// <param name="column">Column position on given line (1-index)</param>
    /// <returns>Overall index</returns>
    public static int GetCharacterIndex(this string str, int line, int column)
    {
        if (line == 1)
        {
            return column - 1;
        }

        var x = 0;
        var count = 2;

        while ((x = str.IndexOf("\n", x + 1)) != -1)
        {
            if (count == line)
            {
                return x + column - 1;
            }

            count++;
        }

        return -1;
    }

    /// <summary>
    /// Given an overall index for the string, return the corresponding line/column it represents based on newlines.
    /// </summary>
    /// <param name="str">Text context.</param>
    /// <param name="index">Character index</param>
    /// <returns>Line, Column Tuple</returns>
    public static (int, int) GetLineColumnIndex(this string str, int index)
    {
        var line = 1;
        var column = 1;

        for (var i = 0; i < index; i++)
        {
            if (str[i] == '\n')
            {
                line++;
                column = 0;
            }

            column++;
        }

        return (line, column);
    }
}
