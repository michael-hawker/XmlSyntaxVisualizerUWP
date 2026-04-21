// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

public static class XmlDocumentSyntaxExtensions
{
    /// <summary>
    /// Walks the parsed tree and returns source character ranges that — when removed
    /// from the original source — should yield a re-parseable, well-formed XML document.
    /// </summary>
    /// <remarks>
    /// Strategy:
    /// <list type="bullet">
    ///   <item>For every node and token that carries diagnostics, find the smallest enclosing
    ///         <em>removable</em> structural node (element, start/end tag, attribute, text,
    ///         comment, CDATA, processing instruction, declaration) with non-zero width and
    ///         take its full span as a candidate removal range.</item>
    ///   <item>When two candidate ranges overlap, the smaller one wins — the larger range
    ///         is almost always a downstream symptom of the smaller, deeper error.</item>
    ///   <item>Each range is widened leftward to consume any whitespace that immediately
    ///         precedes it in the source.</item>
    ///   <item>If an unclosed XML attribute string consumed a tag-closing <c>&gt;</c> or
    ///         <c>/&gt;</c>, the trailing tag-boundary characters are trimmed back out of
    ///         the range so they survive removal. Trim only applies to attribute removals.</item>
    /// </list>
    /// Note: diagnostics are recorded only on the immediate offending node — they do not
    /// propagate up the tree — so the walk cannot be short-circuited at the root.
    /// </remarks>
    public static IReadOnlyList<Range> GetErrorRanges(this XmlDocumentSyntax tree)
        => GetErrorRanges(tree, source: null);

    /// <summary>
    /// Source-aware overload of <see cref="GetErrorRanges(XmlDocumentSyntax)"/>. Pass the
    /// original text to avoid an extra <see cref="SyntaxNode.ToFullString"/> allocation.
    /// </summary>
    public static IReadOnlyList<Range> GetErrorRanges(this XmlDocumentSyntax tree, string source)
    {
        if (tree is null) throw new ArgumentNullException(nameof(tree));
        source ??= tree.ToFullString();

        var raw = new List<RemovalCandidate>();
        CollectAllRemovalCandidates(tree, raw);
        if (raw.Count == 0) return Array.Empty<Range>();

        // Dedup identical (start,end) pairs and order by length so the smallest (deepest cause) wins.
        var seen = new HashSet<long>();
        var unique = new List<RemovalCandidate>(raw.Count);
        foreach (var c in raw)
        {
            if (seen.Add(((long)c.Start << 32) | (uint)c.End)) unique.Add(c);
        }
        unique.Sort(static (a, b) => (a.End - a.Start).CompareTo(b.End - b.Start));

        // Smallest-overlap-wins. N is small in practice; O(N²) is fine.
        var kept = new List<RemovalCandidate>();
        foreach (var c in unique)
        {
            var overlap = false;
            for (var i = 0; i < kept.Count; i++)
            {
                if (kept[i].Start < c.End && c.Start < kept[i].End) { overlap = true; break; }
            }
            if (!overlap) kept.Add(c);
        }

        var refined = new List<Range>(kept.Count);
        foreach (var c in kept)
        {
            if (TryBuildRefinedRange(c, source, out var range)) refined.Add(range);
        }
        refined.Sort(static (a, b) => a.Start.Value.CompareTo(b.Start.Value));
        return refined;
    }

    /// <summary>
    /// Hot-path entry: returns the single smallest refined repair range without allocating
    /// the full candidate list. Refinement (trim/extend) is applied during selection so a
    /// smaller raw candidate that collapses to zero width does not mask a larger one.
    /// </summary>
    internal static bool TryGetSmallestRepairRange(XmlDocumentSyntax tree, string source, out Range range)
    {
        // Sentinel: int.MaxValue means "no candidate found yet".
        var bestStart = 0;
        var bestEnd = 0;
        var bestLen = int.MaxValue;
        FindSmallestRefinedCandidate(tree, source, ref bestStart, ref bestEnd, ref bestLen);

        if (bestLen == int.MaxValue)
        {
            range = default;
            return false;
        }

        range = new Range(bestStart, bestEnd);
        return true;
    }

    /// <summary>
    /// Recursively walks the tree, evaluating every diagnostic-bearing node and token
    /// against the current best (smallest refined) repair candidate. Mirrors
    /// <see cref="CollectAllRemovalCandidates"/> but tracks only the running winner so the
    /// hot path avoids any list/LINQ allocations.
    /// </summary>
    private static void FindSmallestRefinedCandidate(SyntaxNode node, string source, ref int bestStart, ref int bestEnd, ref int bestLen)
    {
        if (node.ContainsDiagnostics) PromoteIfSmaller(node, source, ref bestStart, ref bestEnd, ref bestLen);

        foreach (var child in node.ChildNodes)
        {
            if (child is SyntaxToken token)
            {
                if (token.ContainsDiagnostics) PromoteIfSmaller(token, source, ref bestStart, ref bestEnd, ref bestLen);
            }
            else
            {
                FindSmallestRefinedCandidate(child, source, ref bestStart, ref bestEnd, ref bestLen);
            }
        }
    }

    /// <summary>
    /// Resolves one diagnostic-bearing node/token to its enclosing removable structural
    /// ancestor, refines (trim/extend) the resulting range, and promotes it to "best" if
    /// it is strictly smaller than any previously seen refined candidate. Refining during
    /// selection ensures a candidate that collapses to zero width does not mask a slightly
    /// larger candidate that would refine cleanly.
    /// </summary>
    private static void PromoteIfSmaller(SyntaxNode bearer, string source, ref int bestStart, ref int bestEnd, ref int bestLen)
    {
        var enclosing = FindEnclosingRemovable(bearer);
        if (enclosing is null) return;

        if (!TryBuildRefinedRange(new RemovalCandidate(enclosing), source, out var refined)) return;

        var len = refined.End.Value - refined.Start.Value;
        if (len < bestLen)
        {
            bestStart = refined.Start.Value;
            bestEnd = refined.End.Value;
            bestLen = len;
        }
    }

    /// <summary>
    /// Visualization-path walker: recursively gathers every diagnostic-bearing node/token's
    /// enclosing removable structural ancestor as a raw, unrefined candidate. Caller is
    /// responsible for dedup, overlap pruning, and refinement.
    /// </summary>
    private static void CollectAllRemovalCandidates(SyntaxNode node, List<RemovalCandidate> ranges)
    {
        if (node.ContainsDiagnostics)
        {
            var enclosing = FindEnclosingRemovable(node);
            if (enclosing != null) ranges.Add(new RemovalCandidate(enclosing));
        }

        foreach (var child in node.ChildNodes)
        {
            if (child is SyntaxToken token)
            {
                if (token.ContainsDiagnostics)
                {
                    var enclosing = FindEnclosingRemovable(token);
                    if (enclosing != null) ranges.Add(new RemovalCandidate(enclosing));
                }
            }
            else
            {
                CollectAllRemovalCandidates(child, ranges);
            }
        }
    }

    /// <summary>
    /// Climbs the parent chain from a diagnostic-bearing node/token to the nearest
    /// ancestor that is structurally safe to delete in one piece (an element, tag,
    /// attribute, text, comment, CDATA, PI, or declaration). Skips zero-width
    /// (synthesized "missing") ancestors so we always remove real source characters.
    /// When the climb crossed any zero-width ancestor and lands on an
    /// <see cref="XmlElementSyntax"/>, the element's <see cref="XmlElementSyntax.StartTag"/>
    /// is returned instead of the whole element — the diagnostic was almost certainly a
    /// missing end tag, so removing only the unclosed start tag preserves any valid
    /// child content (it re-parents to the grandparent on the next parse).
    /// </summary>
    private static SyntaxNode FindEnclosingRemovable(SyntaxNode node)
    {
        var skippedZeroWidth = false;
        for (var current = node; current != null; current = current.Parent)
        {
            if (current.FullWidth == 0)
            {
                skippedZeroWidth = true;
                continue;
            }

            switch (current)
            {
                case XmlElementSyntax elem when skippedZeroWidth
                        && elem.StartTag is XmlElementStartTagSyntax start
                        && start.FullWidth > 0:
                    return start;
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

    /// <summary>
    /// Translates a raw removal candidate into a final source range by applying the two
    /// post-processing rules: (1) for attribute candidates, trim a trailing
    /// <c>&gt;</c>/<c>/&gt;</c> back out so the host tag survives; (2) extend leftward
    /// over preceding whitespace so we don't leave orphan indentation behind. Returns
    /// false if refinement collapses the range to zero width.
    /// </summary>
    private static bool TryBuildRefinedRange(RemovalCandidate c, string source, out Range range)
    {
        var start = c.Start;
        var end = c.End;

        if (c.IsAttribute)
        {
            // Attribute strings can swallow the start tag's closing '>' or '/>';
            // trim those back out so the host tag survives removal of the attribute.
            var trimmed = Math.Min(end, source.Length);
            while (trimmed > start && IsXmlWhitespace(source[trimmed - 1])) trimmed--;
            if (trimmed > start && source[trimmed - 1] == '>')
            {
                trimmed--;
                if (trimmed > start && source[trimmed - 1] == '/') trimmed--;
            }
            end = trimmed;
        }

        // Extend left over preceding whitespace so we don't leave orphan indentation.
        while (start > 0 && IsXmlWhitespace(source[start - 1])) start--;

        if (end <= start)
        {
            range = default;
            return false;
        }

        range = new Range(start, end);
        return true;
    }

    // XML 1.0 §2.3 whitespace set (S production), narrowed to the four chars the parser
    // actually emits as trivia.
    private static bool IsXmlWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';

    private readonly struct RemovalCandidate
    {
        public RemovalCandidate(SyntaxNode node)
        {
            Start = node.FullSpan.Start;
            End = node.FullSpan.End;
            IsAttribute = node is XmlAttributeSyntax;
        }

        public int Start { get; }
        public int End { get; }
        public bool IsAttribute { get; }
    }
}
