// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

public static class XmlDocumentSyntaxExtensions
{
    /// <summary>
    /// Walks the parsed tree and returns source character ranges that — when removed
    /// from the original text — should yield a re-parseable, well-formed XML document.
    /// </summary>
    /// <remarks>
    /// Strategy:
    /// <list type="bullet">
    ///   <item>For every node and token that carries diagnostics, find the smallest enclosing
    ///         <em>removable</em> structural node (element, start/end tag, attribute, text,
    ///         comment, CDATA, processing instruction, declaration) with non-zero width and
    ///         take its full span as a candidate removal range.</item>
    ///   <item>When two candidate ranges overlap, the smaller one wins — the larger range
    ///         is almost always a downstream symptom of the smaller, deeper error
    ///         (e.g. an unclosed attribute string makes its enclosing element appear
    ///         "missing an end tag"). Removing the deepest cause and re-parsing usually
    ///         resolves the symptoms automatically.</item>
    ///   <item>Each range is widened leftward to consume any whitespace that immediately
    ///         precedes it in the source (so removing a stray attribute doesn't leave a
    ///         double space behind).</item>
    ///   <item>If an unclosed XML string consumed a tag-closing <c>&gt;</c> or <c>/&gt;</c>,
    ///         the trailing tag-boundary characters are trimmed back out of the range so
    ///         they survive removal.</item>
    /// </list>
    /// Note: diagnostics in this library are recorded only on the immediate offending node —
    /// they do <em>not</em> propagate up the tree — so we cannot short-circuit the walk
    /// based on <see cref="SyntaxNode.ContainsDiagnostics"/> at the document root.
    /// </remarks>
    public static IReadOnlyList<Range> GetErrorRanges(this XmlDocumentSyntax tree)
    {
        if (tree is null) throw new ArgumentNullException(nameof(tree));

        var sourceText = tree.ToFullString();
        var rawRanges = new List<Range>();
        Collect(tree, rawRanges);

        return PostProcess(rawRanges, sourceText);
    }

    private static void Collect(SyntaxNode node, List<Range> ranges)
    {
        if (HasOwnDiagnostics(node))
        {
            var enclosing = FindEnclosingRemovable(node);
            if (enclosing != null)
            {
                ranges.Add(ToRange(enclosing.FullSpan));
            }
        }

        foreach (var child in node.ChildNodes)
        {
            if (child is SyntaxToken token)
            {
                if (HasOwnDiagnostics(token))
                {
                    var enclosing = FindEnclosingRemovable(token);
                    if (enclosing != null)
                    {
                        ranges.Add(ToRange(enclosing.FullSpan));
                    }
                }
            }
            else
            {
                Collect(child, ranges);
            }
        }
    }

    private static bool HasOwnDiagnostics(SyntaxNode node)
    {
        if (!node.ContainsDiagnostics) return false;

        var diags = node.GetDiagnostics();
        return diags != null && diags.Any();
    }

    private static SyntaxNode FindEnclosingRemovable(SyntaxNode node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current.FullWidth == 0)
            {
                continue;
            }

            switch (current)
            {
                case XmlElementSyntax _:
                case XmlEmptyElementSyntax _:
                case XmlElementStartTagSyntax _:
                case XmlElementEndTagSyntax _:
                case XmlAttributeSyntax _:
                case XmlCommentSyntax _:
                case XmlCDataSectionSyntax _:
                case XmlProcessingInstructionSyntax _:
                case XmlDeclarationSyntax _:
                case XmlTextSyntax _:
                    return current;
            }
        }

        return null;
    }

    private static IReadOnlyList<Range> PostProcess(List<Range> raw, string source)
    {
        if (raw.Count == 0) return Array.Empty<Range>();

        // 1. Drop ranges that strictly contain another candidate (smallest cause wins).
        var ordered = raw
            .Select(r => (Start: r.Start.Value, End: r.End.Value))
            .Distinct()
            .OrderBy(r => r.End - r.Start)
            .ToList();

        var kept = new List<(int Start, int End)>();
        foreach (var candidate in ordered)
        {
            var overlapsLarger = kept.Any(k => Overlaps(k, candidate));
            if (!overlapsLarger)
            {
                kept.Add(candidate);
            }
        }

        // 2. Extend each range leftward over preceding whitespace.
        // 3. Trim trailing tag-boundary characters ('>' or '/>') so that an unclosed
        //    attribute string doesn't swallow the start tag's closing punctuation.
        var refined = kept
            .Select(r => TrimTrailingTagBoundary(r, source))
            .Select(r => ExtendLeftOverWhitespace(r, source))
            .Where(r => r.End > r.Start)
            .OrderBy(r => r.Start)
            .Select(r => new Range(r.Start, r.End))
            .ToList();

        return refined;
    }

    private static bool Overlaps((int Start, int End) a, (int Start, int End) b) =>
        a.Start < b.End && b.Start < a.End;

    private static (int Start, int End) ExtendLeftOverWhitespace((int Start, int End) range, string source)
    {
        var start = range.Start;
        while (start > 0 && IsXmlWhitespace(source[start - 1]))
        {
            start--;
        }
        return (start, range.End);
    }

    private static (int Start, int End) TrimTrailingTagBoundary((int Start, int End) range, string source)
    {
        var end = Math.Min(range.End, source.Length);

        // Trim trailing whitespace first so we can examine the meaningful tail.
        while (end > range.Start && IsXmlWhitespace(source[end - 1]))
        {
            end--;
        }

        if (end > range.Start && source[end - 1] == '>')
        {
            end--; // drop '>'
            if (end > range.Start && source[end - 1] == '/')
            {
                end--; // drop the '/' of a self-closing tag too
            }
        }

        return (range.Start, end);
    }

    private static bool IsXmlWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';

    private static Range ToRange(TextSpan span) => new Range(span.Start, span.End);
}
