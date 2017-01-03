using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for HelpDialog.xaml
    /// </summary>
    public partial class HelpDialog : Window
    {
        private static bool _close = false;
        private static readonly HelpDialog HelpDlg = new HelpDialog();

        public static void ShowHelp(string anchor = null)
        {
            HelpDlg.Navigate(anchor);
            if (!HelpDlg.IsVisible)
                HelpDlg.Show();
        }

        public static void CloseHelp()
        {
            _close = true;
            HelpDlg.Close();
        }

        internal HelpDialog()
        {
            InitializeComponent();
            WebBrowser.Navigate(new Uri(@"pack://siteoforigin:,,,/HelpDocumentation.html"));
        }

        private void Navigate(string anchor = null)
        {
            if (anchor == null)
            {
                WebBrowser.Navigate(new Uri(@"pack://siteoforigin:,,,/HelpDocumentation.html"));
                return;
            }

            WebBrowser.Navigate(new Uri($"pack://siteoforigin:,,,/HelpDocumentation.html#{anchor}"));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_close)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            Hide();
        }
    }
}
