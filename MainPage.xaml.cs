using Microsoft.Language.Xml;
using Monaco;
using Monaco.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
        public List<XmlSyntaxData> RootNodes
        {
            get { return (List<XmlSyntaxData>)GetValue(RootNodesProperty); }
            set { SetValue(RootNodesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for RootNode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootNodesProperty =
            DependencyProperty.Register(nameof(RootNodes), typeof(List<XmlSyntaxData>), typeof(MainPage), new PropertyMetadata(new List<XmlSyntaxData>()));

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
            var document = Parser.ParseText(XmlEditor.Text);
            var list = new List<XmlSyntaxData>();
            list.Add(XmlSyntaxData.FromNode(document));
            RootNodes = list;
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

        public List<XmlSyntaxData> Children { get; set; }

        public static XmlSyntaxData FromNode(SyntaxNode node)
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
                Children = node.ChildNodes.Select(child => XmlSyntaxData.FromNode(child)).ToList()
            };
        }
    }

    public class XmlSyntaxError
    {
        public ERRID Id { get; set; }
        public string Description { get; set; }
    }
}
