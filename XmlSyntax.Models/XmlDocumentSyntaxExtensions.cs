using System;
using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

public static class XmlDocumentSyntaxExtensions
{
    public static Range[] GetErrorRanges(this XmlDocumentSyntax tree)
    {
        // TODO: Create own Range-type struct
        // Walk tree and be smart about detecting the error locations and mismatched pairs.
        // Also get start/end, so middle can be OK still... (work through scenarios and expectations)
    }

    // TODO: Create another string extension helper that takes the set of ranges and returns the modified string, use that to get the expected output.
    // TODO: Have a tree pruner that can return a tree without the specified ranges? Needs to be able re-parent, etc...?
}
