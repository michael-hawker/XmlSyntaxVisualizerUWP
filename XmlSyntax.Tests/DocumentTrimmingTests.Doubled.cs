using XmlSyntax.Models;

namespace XmlSyntax.Tests;

/// <summary>
/// "Doubled" variants of every scenario in <c>DocumentTrimmingTests.cs</c>. Each input
/// here contains <strong>two independent instances of the same class of error</strong>
/// in one document — the kind of state a developer might leave behind after a paste,
/// rename, or mass-edit that introduced more than one broken construct simultaneously.
/// <para>
/// The expected outputs reflect what an ideal repair pass would produce
/// (each broken construct minimally repaired, all valid content preserved). Tests in
/// this file may currently fail; failures are diagnostic, not regressions.
/// </para>
/// </summary>
public partial class DocumentTrimmingTests
{
    [TestMethod]
    public void ExtraneousPartialAttribute_Doubled()
    {
        string input =
            """
            <Root>
              <A One="Two" Hel/>
              <B Three="Four" Wor/>
            </Root>
            """;

        string expected_output =
            """
            <Root>
              <A One="Two"/>
              <B Three="Four"/>
            </Root>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousPartialAttributeFullTag_Doubled()
    {
        string input =
            """
            <Root>
              <A One="Two" Hel>
              </A>
              <B Three="Four" Wor>
              </B>
            </Root>
            """;

        string expected_output =
            """
            <Root>
              <A One="Two">
              </A>
              <B Three="Four">
              </B>
            </Root>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousPartialAttributeValue_Doubled()
    {
        string input =
            """
            <Root>
              <A One="Two" Hello="World/>
              <B Three="Four" Goodbye="Cruel/>
            </Root>
            """;

        string expected_output =
            """
            <Root>
              <A One="Two"/>
              <B Three="Four"/>
            </Root>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousPartialAttributeValueFullTag_Doubled()
    {
        string input =
            """
            <Root>
              <A One="Two" Hello="World>
              </A>
              <B Three="Four" Goodbye="Cruel>
              </B>
            </Root>
            """;

        string expected_output =
            """
            <Root>
              <A One="Two">
              </A>
              <B Three="Four">
              </B>
            </Root>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedClosingTag_Doubled()
    {
        string input =
            """
            <A>
            </B>
            </C>
            </A>
            """;

        string expected_output =
            """
            <A>
            </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedOpeningTag_Doubled()
    {
        string input =
            """
            <A>
            <B>
            <C>
            </A>
            """;

        string expected_output =
            """
            <A>
            </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedOpeningTagWithAttributeFull_Doubled()
    {
        string input =
            """
            <X>
              <X/>
              <A.B></A.B>
              <A B="value">
              <A>&#x03C0;</A>
              <C D="other">
              <C>&lt;</C>
            </X>
            """;

        string expected_output =
            """
            <X>
              <X/>
              <A.B></A.B>
              <A>&#x03C0;</A>
              <C>&lt;</C>
            </X>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedFullPropertyTag_Doubled()
    {
        string input =
            """
            <Root>
              <A B="value">
                <A.Something>
              </A>
              <C D="value">
                <C.Other>
              </C>
            </Root>
            """;

        string expected_output =
            """
            <Root>
              <A B="value">
              </A>
              <C D="value">
              </C>
            </Root>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedFullPropertyTagExtended_Doubled()
    {
        string input =
            """
              <A B="value">
                <A.Something>
            	</A
                <A.Another>
            	</A
              </A>
            """;

        string expected_output =
            """
              <A B="value">
              </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedFullPropertyTagExtendedFull_Doubled()
    {
        string input =
            """
            <X>
              <A.B></A.B>
              <A B="value">
                <A.Something>
            	</A
              </A>
              <C D="value">
                <C.Something>
            	</C
              </C>
            </X>
            """;

        string expected_output =
            """
            <X>
              <A.B></A.B>
              <A B="value">
              </A>
              <C D="value">
              </C>
            </X>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }
}
