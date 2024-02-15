// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

/// <summary>
/// Light-weight wrapper around error data.
/// </summary>
public class XmlSyntaxError
{
    public ERRID Id { get; set; }

    public string Description { get; set; }
}
