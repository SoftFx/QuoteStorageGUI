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

namespace QuoteHistoryGUI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindowView 
    {

        private readonly MainWindowModel _model;
        public MainWindowView()
        {
            DataContext = _model = new MainWindowModel();


            InitializeComponent();

            
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            var dlg = new StorageSelectionDialog()
            {
                Owner = Application.Current.MainWindow
            };
            dlg.ShowDialog();
            if(dlg.StoragePath.Text!="")
            _model.StoragePath = dlg.StoragePath.Text;

        }

        private void treeView_Expanded(object sender, RoutedEventArgs e)
        {
            _model.Expand(sender, e);
        }
    }
}
