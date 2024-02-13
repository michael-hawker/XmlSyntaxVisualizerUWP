// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Language.Xml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Xml.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using WinUIEditor;
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

        public int Column
        {
            get { return (int)GetValue(ColumnProperty); }
            set { SetValue(ColumnProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Column.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ColumnProperty =
            DependencyProperty.Register(nameof(Column), typeof(int), typeof(MainPage), new PropertyMetadata(0));

        public int LineNumber
        {
            get { return (int)GetValue(LineNumberProperty); }
            set { SetValue(LineNumberProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LineNumber.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineNumberProperty =
            DependencyProperty.Register(nameof(LineNumber), typeof(int), typeof(MainPage), new PropertyMetadata(0));

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
        private bool _isLightMode = false;

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
            XmlEditor.DefaultColorsChanged += XmlEditor_DefaultColorsChanged;
            XmlEditor.Editor.Modified += Editor_Modified;
            XmlEditor.Editor.UpdateUI += Editor_UpdateUI;
            XmlEditor.Editor.MouseDwellTime = 500;
            XmlEditor.Editor.DwellStart += Editor_DwellStart;

            // Load example file
            using (var stream = GetType().Assembly.GetManifestResourceStream("XmlSyntaxVisualizerUwp.ExampleDocument.xml"))
            using (var reader = new StreamReader(stream))
            {
                XmlEditor.Editor.SetText(reader.ReadToEnd());
            }
        }

        private void XmlEditor_DefaultColorsChanged(object sender, ElementTheme e)
        {
            _isLightMode = e == ElementTheme.Light;
        }

        private void Editor_Modified(WinUIEditor.Editor sender, WinUIEditor.ModifiedEventArgs args)
        {
            if (((ModificationFlags)args.ModificationType).HasFlag(ModificationFlags.InsertText)
                || ((ModificationFlags)args.ModificationType).HasFlag(ModificationFlags.DeleteText))
            {
                // text has changed
                XmlEditor_TextChanged();
            }
        }

        private void Editor_UpdateUI(Editor sender, UpdateUIEventArgs args)
        {
            // TODO: Seeing this called when interacting with the TreeView causing the TreeView to scroll awkwardly with the logic we have in UpdateCurrentInfo()...
            if (((Update)args.Updated).HasFlag(Update.Content)
                || ((Update)args.Updated).HasFlag(Update.Selection))
            {
                // Cursor moved in editor
                UpdateCurrentInfo(); 
            }
        }

        #region Update and Parsing
        private void XmlEditor_TextChanged()
        {
            // Text changed, we need to clear everything and re-parse
            XmlEditor.Editor.IndicatorClearRange(0, XmlEditor.Editor.Length);

            // Actually invoke the XmlParser Parser
            _lastRoot = Parser.ParseText(XmlEditor.Editor.GetText(XmlEditor.Editor.Length));

            // Translate our parsed tree to our set of UI-ready nodes
            var list = new List<XmlSyntaxData>
            {
                XmlSyntaxData.FromNode(_lastRoot)
            };
            RootNodes = list;

            // Update our current location's status
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                UpdateCurrentInfo();
            });

            // Update the validation status in the Editor
            var validRoot = XmlParserHelpers.GetValidXmlTree(XmlEditor.Editor.GetText(XmlEditor.Editor.Length));

            ValidXmlDoc.Text = validRoot.ToFullString();
        }

        /// <summary>
        /// This function just provides a bit of info about the Xml Node that's at the caret position in the Editor.
        /// </summary>
        private void UpdateCurrentInfo()
        {
            // Figure out where we are.
            var index = XmlEditor.Editor.CurrentPos;

            if (index == -1)
            {
                return;
            }

            LineNumber = (int)XmlEditor.Editor.LineFromPosition(index) + 1;
            Column = (int)XmlEditor.Editor.GetColumn(index) + 1;

            // Use the caret position (as index) to find the corresponding Xml Node from our parsed tree.
            var raw_node = _lastRoot.FindNode((int)index);
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
                //// var (line_s, col_s) = (XmlEditor.Editor.LineFromPosition(node.SpanStart), XmlEditor.Editor.GetColumn(node.SpanStart));
                //// var (line_e, col_e) = (XmlEditor.Editor.LineFromPosition(node.SpanEnd - 1), XmlEditor.Editor.GetColumn(node.SpanEnd - 1));

                // Provide info in our UI box.
                ElementInfo = node.Text + Environment.NewLine;
                ElementInfo += node.Type + Environment.NewLine;
                ElementInfo += "Parent:" + parent?.Type + Environment.NewLine;
                ElementInfo += "Parent Element:" + raw_node?.ParentElement?.Name + Environment.NewLine;
            }
        }
        #endregion

        #region Monaco -> TreeView Helpers
        private void Editor_DwellStart(Editor sender, DwellStartEventArgs args)
        {
            // Figure out where we our in relation to our Xml Tree
            var index = args.Position;
            
            if (index == -1)
            {
                return;
            }

            // Get that node from the Xml Parser and wrap it in a friendly container.
            var node = XmlSyntaxData.FromNode(_lastRoot.FindNode(index + 1), false);

            if (node != null)
            {
                // Refetch proper line/col from start of token
                var (line_s, col_s) = (XmlEditor.Editor.LineFromPosition(node.SpanStart), XmlEditor.Editor.GetColumn(node.SpanStart));
                //// var (line_e, col_e) = (XmlEditor.Editor.LineFromPosition(node.SpanEnd - 1), XmlEditor.Editor.GetColumn(node.SpanEnd - 1));

                // TODO: This doesn't seem to be working/displaying?
                // Provide nice UI on hover to show more info about the node.
                XmlEditor.Editor.CallTipShow(index, 
                                             "*" + node.Type + "* " + node.Text + " [" + node.SpanStart + ".." + node.SpanEnd + ")\n" +
                                             "Line: " + line_s + " Col: " + col_s + " Length: " + node.Length);
            }

            return;
        }

        private void TreeView_ScrollNode(SyntaxNode raw_node)
        {
            // We need to find the UI container that contains the same node as the one we have here
            var node = FindNode(RootNodes, raw_node.GetHashCode());
            var container = XmlSyntaxTree.ContainerFromItem(node) as MUX.TreeViewItem;

            if (container != null)
            {
                container.StartBringIntoView(new BringIntoViewOptions()
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

                ////var (line_s, col_s) = (XmlEditor.Editor.LineFromPosition(data.SpanStart), XmlEditor.Editor.GetColumn(data.SpanStart));
                ////var (line_e, col_e) = (XmlEditor.Editor.LineFromPosition(data.SpanEnd - 1), XmlEditor.Editor.GetColumn(data.SpanEnd - 1));

                XmlEditor.Editor.IndicatorClearRange(0, XmlEditor.Editor.Length);

                XmlEditor.Editor.IndicatorCurrent = 0;
                XmlEditor.Editor.IndicSetStyle(0, IndicatorStyle.FullBox);
                XmlEditor.Editor.IndicSetFore(0, 0x00FFFF); // Yellow (BGR)
                if (_isLightMode)
                {
                    XmlEditor.Editor.IndicSetAlpha(0, Alpha.Opaque);
                }
                // TODO: else -> should set to a transparent alpha value (whatever the default is?)
                XmlEditor.Editor.IndicSetUnder(0, true);
                XmlEditor.Editor.IndicatorFillRange(data.SpanStart, data.SpanEnd - data.SpanStart);

                ////Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges
            }
        }

        private void XmlSyntaxTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            // If we click on the TreeViewItem we've moused over, then move the caret to that location
            // to update our UI info box.
            if (_lastData != null)
            {
                var line_s = XmlEditor.Editor.LineFromPosition(_lastData.SpanStart);

                XmlEditor.Editor.FirstVisibleLine = Math.Max(line_s - XmlEditor.Editor.LinesOnScreen / 2, 0);
            }
        }
        #endregion
    }
}
