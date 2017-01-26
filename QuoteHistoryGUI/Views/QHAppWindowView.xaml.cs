using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using QuoteHistoryGUI.Models;
using QuoteHistoryGUI.Dialogs;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;

namespace QuoteHistoryGUI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class QHAppWindowView : Window
    {

        private readonly QHAppWindowModel _model;
        public QHAppWindowView()
        {        
            DataContext = _model = new QHAppWindowModel(this.Dispatcher);
            InitializeComponent();
            this.Closed += OnClosed;
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            mainWindow = this as Window;
            var dlg = new StorageSelectionDialog()
            {
                Owner = mainWindow
            };
            dlg.ShowDialog();
            if (dlg.StoragePath.Text != "")
            {
                if (dlg.StoragePath.Text != "")
                {
                    var tab = new StorageInstanceModel(dlg.StoragePath.Text, this.Dispatcher, _model.Interactor, (bool)dlg.ReadOnlyBox.IsChecked?StorageInstanceModel.OpenMode.ReadOnly:StorageInstanceModel.OpenMode.ReadWrite);
                    if (tab.Status == "Ok")
                        _model.TryToAddStorage(tab);
                    else MessageBox.Show(this, "Can't open storage\n\nMessage: " + tab.Status, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);
                }
            }
        }
        private LoadingDialog _loadingDlg;
        private Thread loadingThread;
        private Window mainWindow;
        double xpos;
        double ypos;

        private void loadingThreadWork()
        {
            try
            {
                if(_loadingDlg == null)
                    _loadingDlg = new LoadingDialog();
                _loadingDlg.Left = xpos; _loadingDlg.Top = ypos;
                _loadingDlg.Show();
                System.Windows.Threading.Dispatcher.Run();
            }
            catch
            {
            }
            
        }

        protected void OnClosed(object sender, EventArgs e)
        {
            if(_loadingDlg?.Dispatcher!=null)
            _loadingDlg.Dispatcher.InvokeShutdown();
        }

        public  void ShowLoading()
        {
            var screenCoord = this.PointToScreen(new Point(0, 0));
            xpos = screenCoord.X + this.ActualWidth/2-20;
            ypos = screenCoord.Y + this.ActualHeight / 2-40;
            if (_loadingDlg == null)
            {
                loadingThread = new Thread(new ThreadStart(loadingThreadWork));
                loadingThread.SetApartmentState(ApartmentState.STA);
                loadingThread.IsBackground = true;
                loadingThread.Start();
            }
            else
                _loadingDlg.Dispatcher.Invoke(delegate { _loadingDlg.Left = xpos; _loadingDlg.Top = ypos; _loadingDlg.Show(); _loadingDlg.Activate(); });
        }



        public void HideLoading()
        {
            if (_loadingDlg != null)
                _loadingDlg.Dispatcher.Invoke(delegate { _loadingDlg.Hide(); });
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            HelpDialog.CloseHelp();
            base.OnClosing(e);
        }
    }
}
