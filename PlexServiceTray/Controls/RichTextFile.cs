using System;
using System.Diagnostics;
using System.IO;
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

        private void HandleHyperlinkClick(object inSender, RoutedEventArgs inArgs) {
            if (!OpenLinksInBrowser) {
                return;
            }

            if (inArgs.Source is not Hyperlink link) {
                return;
            }

            Process.Start(link.NavigateUri.ToString());
            inArgs.Handled = true;
        }

        #region Properties
        public bool OpenLinksInBrowser { get; set; }

        public string File
        {
            get => (string)GetValue(FileProperty);
            set => SetValue(FileProperty, value);
        }

        public static readonly DependencyProperty FileProperty =
            DependencyProperty.Register("File", typeof(string), typeof(RichTextFile),
            new PropertyMetadata(OnFileChanged));

        private static void OnFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rtf = d as RichTextFile;
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
                var range = new TextRange(inFlowDocument.ContentStart, inFlowDocument.ContentEnd);
                var fStream = new FileStream(inFilename, FileMode.Open, FileAccess.Read, FileShare.Read);

                range.Load(fStream, DataFormats.Rtf);
                fStream.Close();
            }
        }
        #endregion
    }
}
