using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PlexServiceTray.Controls
{
    internal class RichTextFile : RichTextBox
    {
        public RichTextFile()
        {
            AddHandler(Hyperlink.RequestNavigateEvent, new RoutedEventHandler(HandleHyperlinkClick));
        }

        private void HandleHyperlinkClick(object inSender, RoutedEventArgs inArgs)
        {
            if (OpenLinksInBrowser)
            {
                Hyperlink link = inArgs.Source as Hyperlink;
                if (link != null)
                {
                    Process.Start(link.NavigateUri.ToString());
                    inArgs.Handled = true;
                }
            }
        }

        #region Properties
        public bool OpenLinksInBrowser { get; set; }

        public String File
        {
            get { return (string)GetValue(FileProperty); }
            set { SetValue(FileProperty, value); }
        }

        public static DependencyProperty FileProperty =
            DependencyProperty.Register("File", typeof(String), typeof(RichTextFile),
            new PropertyMetadata(OnFileChanged));

        private static void OnFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            RichTextFile rtf = d as RichTextFile;
            if (rtf == null)
                return;

            ReadFile(rtf.File, rtf.Document);
        }
        #endregion

        #region Functions
        private static void ReadFile(string inFilename, FlowDocument inFlowDocument)
        {
            if (System.IO.File.Exists(inFilename))
            {
                TextRange range = new TextRange(inFlowDocument.ContentStart, inFlowDocument.ContentEnd);
                FileStream fStream = new FileStream(inFilename, FileMode.Open, FileAccess.Read, FileShare.Read);

                range.Load(fStream, DataFormats.Rtf);
                fStream.Close();
            }
        }
        #endregion
    }
}
