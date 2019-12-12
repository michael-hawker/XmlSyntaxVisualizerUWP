using Microsoft.Language.Xml;
using Monaco;
using Monaco.Editor;
using Monaco.Helpers;
using Monaco.Languages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace XmlSyntaxVisualizerUwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Properties
        public List<XmlSyntaxData> RootNodes
        {
            get { return (List<XmlSyntaxData>)GetValue(RootNodesProperty); }
            set { SetValue(RootNodesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for RootNode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootNodesProperty =
            DependencyProperty.Register(nameof(RootNodes), typeof(List<XmlSyntaxData>), typeof(MainPage), new PropertyMetadata(new List<XmlSyntaxData>()));

        private SyntaxNode _lastRoot;
        private XmlSyntaxData _lastData;
        #endregion

        #region Style Resources
        private readonly CssLineStyle HighlightStyle = new CssLineStyle()
        {
            BackgroundColor = new SolidColorBrush(Colors.Yellow),
        };

        private static readonly SolidColorBrush ListBrush = new SolidColorBrush(Colors.Cyan);
        private static readonly SolidColorBrush TokenBrush = new SolidColorBrush(Colors.LightGreen);
        private static readonly SolidColorBrush SyntaxBrush = new SolidColorBrush(Colors.MediumPurple);
        private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Colors.Black);

        private static SolidColorBrush ColorSelector(string typeclass)
        {
            switch (typeclass)
            {
                case "list":
                    return ListBrush;
                case "token":
                    return TokenBrush;
                case "syntax":
                    return SyntaxBrush;
                default:
                    return DefaultBrush;
            }
        }
        #endregion

        public MainPage()
        {
            this.InitializeComponent();

            XmlEditor.RegisterPropertyChangedCallback(CodeEditor.TextProperty, XmlEditor_TextChanged);
            
            // Load example file
            using (var stream = GetType().Assembly.GetManifestResourceStream("XmlSyntaxVisualizerUwp.ExampleDocument.xml"))
            using (var reader = new StreamReader(stream))
            {
                XmlEditor.Text = reader.ReadToEnd();
            }
        }

        private void XmlEditor_TextChanged(DependencyObject sender, DependencyProperty dp)
        {
            XmlEditor.Decorations.Clear();

            _lastRoot = Parser.ParseText(XmlEditor.Text);

            var list = new List<XmlSyntaxData>();
            list.Add(XmlSyntaxData.FromNode(_lastRoot));
            RootNodes = list;
        }

        #region Hover/Highlighting
        private async void XmlEditor_Loading(object sender, RoutedEventArgs e)
        {
            var languages = new Monaco.LanguagesHelper(XmlEditor);

            await languages.RegisterHoverProviderAsync("xml", (model, position) =>
            {
                return AsyncInfo.Run(async delegate (CancellationToken cancelationToken)
                {
                    var index = XmlEditor.Text.GetCharacterIndex((int)position.LineNumber, (int)position.Column);

                    if (index == -1)
                    {
                        return default;
                    }

                    var node = XmlSyntaxData.FromNode(_lastRoot.FindNode(index + 1), false);

                    if (node != null)
                    {
                        // Refetch proper line/col from start of token
                        var (line_s, col_s) = XmlEditor.Text.GetLineColumnIndex(node.SpanStart);
                        var (line_e, col_e) = XmlEditor.Text.GetLineColumnIndex(node.SpanEnd - 1);

                        return new Hover(new string[]
                            {
                                "*" + node.Type + "* " + node.Text + " [" + node.SpanStart + ".." + node.SpanEnd + ")",
                                "Line: " + line_s + " Col: " + col_s + " Length: " + node.Length
                            }, 
                            new Range((uint)line_s, (uint)col_s, (uint)line_e, (uint)col_e + 1));
                    }

                    return default;
                });
            });
        }

        private void TreeViewItem_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.TreeViewItem item &&
                item.DataContext is XmlSyntaxData data &&
                data != _lastData)
            {
                _lastData = data; // Cache so we only update on initial mouse change

                var (line_s, col_s) = XmlEditor.Text.GetLineColumnIndex(data.SpanStart);
                var (line_e, col_e) = XmlEditor.Text.GetLineColumnIndex(data.SpanEnd - 1);

                XmlEditor.Decorations.Clear();

                XmlEditor.Decorations.Add(
                    new IModelDeltaDecoration(new Range((uint)line_s, (uint)col_s, (uint)line_e, (uint)(col_e + 1)), new IModelDecorationOptions()
                {
                    ClassName = HighlightStyle,
                    Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges
                }));
            }
        }
        #endregion
    }

    public class XmlSyntaxData
    {
        public string Type { get; set; }
        public string TypeClass { get; set; }
        public string Text { get; set; }
        public List<XmlSyntaxError> Errors { get; set; }
        public int SpanStart { get; set; }
        public int SpanEnd { get; set; }

        public int Length => SpanEnd - SpanStart;
        public bool IsError => Errors != null && Errors.Count() > 0;
        public string ErrorText => IsError ? string.Join(' ', Errors.Select(e => e.Id.ToString().Substring(4) + ": " + e.Description)) : string.Empty;

        public List<XmlSyntaxData> Children { get; set; }

        public static XmlSyntaxData FromNode(SyntaxNode node, bool withChildren = true)
        {
            return new XmlSyntaxData()
            {
                Type = node.IsList ? "SyntaxList" : node.GetType().Name,
                TypeClass = node.IsList ? "list" : (node.IsToken ? "token" : "syntax"),
                Text = node.IsToken ? (node as SyntaxToken).Text : string.Empty,
                Errors = node.ContainsDiagnostics ?
                    node.GetDiagnostics().Select(d => new XmlSyntaxError()
                    {
                        Id = d.ErrorID,
                        Description = d.GetDescription()
                    }).ToList()
                    : Array.Empty<XmlSyntaxError>().ToList(),
                SpanStart = node.FullSpan.Start,
                SpanEnd = node.FullSpan.End,
                Children = withChildren ? node.ChildNodes.Select(child => XmlSyntaxData.FromNode(child)).ToList() : Array.Empty<XmlSyntaxData>().ToList()
            };
        }
    }

    public class XmlSyntaxError
    {
        public ERRID Id { get; set; }
        public string Description { get; set; }
    }
}
