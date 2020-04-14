// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Language.Xml;
using Monaco;
using Monaco.Editor;
using Monaco.Helpers;
using Monaco.Languages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

using MUX = Microsoft.UI.Xaml.Controls;

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

        // Trackers for what we've last parsed
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

        /// <summary>
        /// Basic converter in order to highlight our TreeViewItem Nodes in the right color to match what type of Xml Node they are.
        /// </summary>
        /// <param name="typeclass">Name of Xml Node class</param>
        /// <returns><see cref="SolidColorBrush"/> for that type of node.</returns>
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

            // Register for changes to Editor text or position.
            XmlEditor.RegisterPropertyChangedCallback(CodeEditor.TextProperty, XmlEditor_TextChanged);
            XmlEditor.RegisterPropertyChangedCallback(CodeEditor.SelectedRangeProperty, XmlEditor_RangeChanged);
            
            // Load example file
            using (var stream = GetType().Assembly.GetManifestResourceStream("XmlSyntaxVisualizerUwp.ExampleDocument.xml"))
            using (var reader = new StreamReader(stream))
            {
                XmlEditor.Text = reader.ReadToEnd();
            }
        }

        #region Update and Parsing
        private void XmlEditor_RangeChanged(DependencyObject sender, DependencyProperty dp)
        {
            UpdateCurrentInfo(); // Cursor moved in editor
        }

        private void XmlEditor_TextChanged(DependencyObject sender, DependencyProperty dp)
        {
            // Text changed, we need to clear everything and re-parse
            XmlEditor.Decorations.Clear();

            // Actually invoke the XmlParser Parser
            _lastRoot = Parser.ParseText(XmlEditor.Text);

            // Translate our parsed tree to our set of UI-ready nodes
            var list = new List<XmlSyntaxData>();
            list.Add(XmlSyntaxData.FromNode(_lastRoot));
            RootNodes = list;

            // Update our current location's status
            UpdateCurrentInfo();
        }

        /// <summary>
        /// This function just provides a bit of info about the Xml Node that's at the caret position in the Editor.
        /// </summary>
        private async void UpdateCurrentInfo()
        {
            // Figure out where we are.
            CurrentPosition = await XmlEditor.GetPositionAsync();

            if (CurrentPosition == null)
            {
                return;
            }

            // Break this down to convert between the Monaco editor and the index our Xml Parser knows about.
            var index = XmlEditor.Text.GetCharacterIndex((int)CurrentPosition.LineNumber, (int)CurrentPosition.Column);

            if (index == -1)
            {
                return;
            }

            // Use the caret position (as index) to find the corresponding Xml Node from our parsed tree.
            var raw_node = _lastRoot.FindNode(index + 1);
            var node = XmlSyntaxData.FromNode(raw_node, false); // Translate to our UI-Friendly object
            XmlSyntaxData parent = null;
            if (raw_node.Parent != null)
            {
                parent = XmlSyntaxData.FromNode(raw_node.Parent, false); // Do the same for the Parent (if we have one)
            }

            TreeView_ScrollNode(raw_node); // Show Item in Tree

            if (node != null)
            {
                // Refetch proper line/col from start of token (as it may start earlier than where the caret is)
                var (line_s, col_s) = XmlEditor.Text.GetLineColumnIndex(node.SpanStart); // Translate from Xml Parser to Monaco positions.
                var (line_e, col_e) = XmlEditor.Text.GetLineColumnIndex(node.SpanEnd - 1);

                // Provide info in our UI box.
                ElementInfo = node.Text + Environment.NewLine;
                ElementInfo += node.Type + Environment.NewLine;
                ElementInfo += "Parent:" + parent?.Type + Environment.NewLine;
                ElementInfo += "Parent Element:" + raw_node?.ParentElement?.Name + Environment.NewLine;
            }
        }
        #endregion

        #region Monaco -> TreeView Helpers
        private async void XmlEditor_Loading(object sender, RoutedEventArgs e)
        {
            var languages = new Monaco.LanguagesHelper(XmlEditor);

            await languages.RegisterHoverProviderAsync("xml", (model, position) =>
            {
                return AsyncInfo.Run(async delegate (CancellationToken cancelationToken)
                {
                    // Figure out where we our in relation to our Xml Tree
                    var index = XmlEditor.Text.GetCharacterIndex((int)position.LineNumber, (int)position.Column);

                    if (index == -1)
                    {
                        return default;
                    }

                    // Get that node from the Xml Parser and wrap it in a friendly container.
                    var node = XmlSyntaxData.FromNode(_lastRoot.FindNode(index + 1), false);

                    if (node != null)
                    {
                        // Refetch proper line/col from start of token
                        var (line_s, col_s) = XmlEditor.Text.GetLineColumnIndex(node.SpanStart);
                        var (line_e, col_e) = XmlEditor.Text.GetLineColumnIndex(node.SpanEnd - 1);

                        // Provide nice UI on hover to show more info about the node.
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

        private void TreeView_ScrollNode(SyntaxNode raw_node)
        {
            // We need to find the UI container that contains the same node as the one we have here
            var node = FindNode(RootNodes, raw_node.GetHashCode());
            var container = XmlSyntaxTree.ContainerFromItem(node) as MUX.TreeViewItem;

            if (container != null)
            {
                container?.StartBringIntoView(new BringIntoViewOptions()
                {
                    VerticalAlignmentRatio = 0.5f
                });

                container.IsSelected = true;
            }
        }

        private XmlSyntaxData FindNode(List<XmlSyntaxData> nodes, int id)
        {
            foreach(var node in nodes)
            {
                if (node.HashId == id)
                {
                    return node;
                }
                else if (node.Children.Count > 0)
                {
                    var value = FindNode(node.Children, id);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            return null;
        }
        #endregion

        #region TreeView -> Monaco Helpers
        private void TreeViewItem_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // When we move our mouse over a node in the TreeView, highlight that Xml Node in our editor with a highlighter.
            if (sender is Microsoft.UI.Xaml.Controls.TreeViewItem item &&
                item.DataContext is XmlSyntaxData data &&
                data != _lastData)
            {
                _lastData = data; // Cache so we only update Editor on initial mouse change

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

        private async void XmlSyntaxTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            // If we click on the TreeViewItem we've moused over, then move the caret to that location
            // to update our UI info box.
            if (_lastData != null)
            {
                var (line_s, col_s) = XmlEditor.Text.GetLineColumnIndex(_lastData.SpanStart);

                await XmlEditor.SetPositionAsync(new Position((uint)line_s, (uint)col_s));
            }
        }
        #endregion
    }
}
