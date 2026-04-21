// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

/// <summary>
/// Hot-path repair-range finder consumed by <see cref="XmlParserHelpers.GetValidXml"/>.
/// </summary>
/// <remarks>
/// Strategy for one repair iteration:
/// <list type="bullet">
///   <item>For every node and token that carries a diagnostic, find the smallest enclosing
///         <em>removable</em> structural node (element, start/end tag, attribute, text,
///         comment, CDATA, processing instruction, declaration) with non-zero width.</item>
///   <item>If the climb crossed any zero-width ("missing") ancestor and lands on an
///         <see cref="XmlElementSyntax"/>, target only its <see cref="XmlElementSyntax.StartTag"/>
///         instead of the whole element — the diagnostic was a missing end tag, so removing
///         only the unclosed start tag preserves valid child content (it re-parents to the
///         grandparent on the next reparse).</item>
///   <item>For attribute removals, trim a swallowed trailing <c>&gt;</c>/<c>/&gt;</c> back
///         out so the host tag survives.</item>
///   <item>Extend each range leftward over preceding whitespace so we don't leave orphan
///         indentation behind.</item>
///   <item>Across all candidates, the smallest refined range wins — a smaller (deeper)
///         cause is almost always behind any overlapping symptom diagnostics, and removing
///         it lets the next reparse resolve the symptoms automatically.</item>
/// </list>
/// Note: diagnostics are recorded only on the immediate offending node — they do not
/// propagate up the tree — so the walk cannot be short-circuited at the root.
/// </remarks>
internal static class XmlDocumentSyntaxExtensions
{
    /// <summary>
    /// Returns the single smallest refined repair range without allocating intermediate
    /// lists. Refinement (trim/extend) is applied during selection so a candidate that
    /// collapses to zero width does not mask a slightly larger candidate elsewhere.
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
    /// against the current best (smallest refined) repair candidate. Tracks only the
    /// running winner so the hot path avoids any list/LINQ allocations.
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
    /// strictly smaller than any previously seen refined candidate.
    /// </summary>
    private static void PromoteIfSmaller(SyntaxNode bearer, string source, ref int bestStart, ref int bestEnd, ref int bestLen)
    {
        var enclosing = FindEnclosingRemovable(bearer);
        if (enclosing is null) return;

        if (!TryBuildRefinedRange(enclosing, source, out var refined)) return;

        var len = refined.End.Value - refined.Start.Value;
        if (len < bestLen)
        {
            bestStart = refined.Start.Value;
            bestEnd = refined.End.Value;
            bestLen = len;
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
    /// Translates a removable node into a final source range by applying the two
    /// post-processing rules: (1) for attribute candidates, trim a trailing
    /// <c>&gt;</c>/<c>/&gt;</c> back out so the host tag survives; (2) extend leftward
    /// over preceding whitespace so we don't leave orphan indentation behind. Returns
    /// false if refinement collapses the range to zero width.
    /// </summary>
    private static bool TryBuildRefinedRange(SyntaxNode removable, string source, out Range range)
    {
        var start = removable.FullSpan.Start;
        var end = removable.FullSpan.End;

        if (removable is XmlAttributeSyntax)
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
}
