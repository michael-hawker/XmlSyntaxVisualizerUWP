// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

        public Position CurrentPosition
        {
            get { return (Position)GetValue(CurrentPositionProperty); }
            set { SetValue(CurrentPositionProperty, value); }
        }

        public static readonly DependencyProperty CurrentPositionProperty =
            DependencyProperty.Register(nameof(CurrentPosition), typeof(Position), typeof(MainPage), new PropertyMetadata(null));

        public string ElementInfo
        {
            get { return (string)GetValue(ElementInfoProperty); }
            set { SetValue(ElementInfoProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ElementInfo.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ElementInfoProperty =
            DependencyProperty.Register(nameof(ElementInfo), typeof(string), typeof(MainPage), new PropertyMetadata(string.Empty));

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

            XmlEditor_KeyDown(null, null); // Trigger position change
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

        private static readonly int[] NonCharacterCodes = new int[] {
            // Modifier Keys
            16, 17, 18, 20, 91,
            // Esc / Page Keys / Home / End / Insert
            27, 33, 34, 35, 36, 45,
            // Arrow Keys
            37, 38, 39, 40,
            // Function Keys
            112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123
        };

        private async void XmlEditor_KeyDown(CodeEditor sender, WebKeyEventArgs args)
        {
            // TODO: Also update on mouse click, not currently supported via wrapper component.
            if (args == null || NonCharacterCodes.Contains(args.KeyCode))
            {
                CurrentPosition = await XmlEditor.GetPositionAsync();

                UpdateCurrentInfo();
            }
        }

        private void UpdateCurrentInfo()
        {
            if (CurrentPosition == null)
            {
                return;
            }

            var index = XmlEditor.Text.GetCharacterIndex((int)CurrentPosition.LineNumber, (int)CurrentPosition.Column);

            if (index == -1)
            {
                return;
            }

            var raw_node = _lastRoot.FindNode(index + 1);
            var node = XmlSyntaxData.FromNode(raw_node, false);
            var parent = XmlSyntaxData.FromNode(raw_node.Parent, false);

            if (node != null)
            {
                // Refetch proper line/col from start of token
                var (line_s, col_s) = XmlEditor.Text.GetLineColumnIndex(node.SpanStart);
                var (line_e, col_e) = XmlEditor.Text.GetLineColumnIndex(node.SpanEnd - 1);

                ElementInfo = node.Text + Environment.NewLine;
                ElementInfo += node.Type + Environment.NewLine;
                ElementInfo += "Parent:" + parent.Type + Environment.NewLine;
                ElementInfo += "Parent Element:" + raw_node?.ParentElement?.Name + Environment.NewLine;
            }
        }
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
