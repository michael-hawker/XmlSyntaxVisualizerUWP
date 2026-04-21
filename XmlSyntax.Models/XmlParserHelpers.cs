// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Language.Xml;
using System.Collections.Generic;

namespace XmlSyntax.Models;

public static class XmlParserHelpers
{
    private const int MaxIterations = 20;

    /// <summary>
    /// Parses <paramref name="text"/> and, if the result has parser diagnostics, iteratively
    /// removes the smallest enclosing structural node whose tokens carry diagnostics until
    /// the document re-parses cleanly (or no further progress can be made).
    /// </summary>
    /// <remarks>
    /// Uses a delete-only repair strategy: only characters from the source are removed, never
    /// added or rewritten. Each iteration removes only the single smallest candidate range
    /// reported by <see cref="XmlDocumentSyntaxExtensions.GetErrorRanges"/> so that downstream
    /// "symptom" diagnostics (e.g. a real closing tag the parser misinterpreted because of
    /// an earlier missing tag) get a chance to re-evaluate against the cleaned text rather
    /// than being deleted as if they were the cause.
    /// </remarks>
    public static XmlDocumentSyntax GetValidXmlTree(string text)
    {
        text ??= string.Empty;

        var tree = Parser.ParseText(text);

        for (var i = 0; i < MaxIterations; i++)
        {
            var ranges = tree.GetErrorRanges();
            if (ranges.Count == 0)
            {
                break;
            }

            var smallest = SmallestRange(ranges);
            var cleaned = text.RemoveRanges(new[] { smallest });
            if (cleaned.Length == text.Length)
            {
                // Guard against an infinite loop on pathological zero-width-only diagnostics.
                break;
            }

            text = cleaned;
            tree = Parser.ParseText(text);
        }

        return tree;
    }

    private static Range SmallestRange(IReadOnlyList<Range> ranges)
    {
        var best = ranges[0];
        var bestLen = best.End.Value - best.Start.Value;
        for (var i = 1; i < ranges.Count; i++)
        {
            var len = ranges[i].End.Value - ranges[i].Start.Value;
            if (len < bestLen)
            {
                best = ranges[i];
                bestLen = len;
            }
        }
        return best;
    }
}
