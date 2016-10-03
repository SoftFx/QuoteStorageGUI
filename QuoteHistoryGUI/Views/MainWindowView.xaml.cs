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

namespace QuoteHistoryGUI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindowView : Window
    {

        private readonly MainWindowModel _model;
        public MainWindowView()
        {           
            DataContext = _model = new MainWindowModel();
            InitializeComponent();

            //_loadingDlg.Owner = this;
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            var dlg = new StorageSelectionDialog()
            {
                Owner = this as Window
            };
            _loadingDlg = new LoadingDialog()
            {
                Owner = Application.Current.MainWindow
            };
            dlg.ShowDialog();
            if (dlg.StoragePath.Text != "")
            {
                if (dlg.StoragePath.Text != "")
                {
                    var tab = new StorageInstance(dlg.StoragePath.Text, _model.Interactor);
                    if (tab.Status == "Ok")
                        _model.StorageTabs.Add(tab);
                    else MessageBox.Show("Can't open storage\n\nMessage: " + tab.Status, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);
                }
            }

        }
        private LoadingDialog _loadingDlg; 
        public  void ShowLoading()
        {
            _loadingDlg.Close();
            _loadingDlg = new LoadingDialog()
            {
                Owner = Application.Current.MainWindow
            };
            this.IsEnabled = false;
            _loadingDlg.Show();
 
        }

        public void HideLoading()
        {
            _loadingDlg.Hide();
            this.IsEnabled = true;
        }
    }
}
