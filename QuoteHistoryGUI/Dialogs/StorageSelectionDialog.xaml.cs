using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Configuration;
using System.ComponentModel;
using log4net;


namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for StorageSelectionDialog.xaml
    /// </summary>
    public partial class StorageSelectionDialog : Window
    {

        bool _canceled = true;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        
        public StorageSelectionDialog()
        {
            try
            {
                log.Info("Storage selection dialog initializing");
                InitializeComponent();
                this.Closing += Window_Closing;
                
                PathBox.ItemsSource = AppConfigManager.GetPathes();
                log.Info("Storage selection dialog initialized");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        private void Browse(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            { };
            if (StoragePath.Text != "")
                dlg.SelectedPath = StoragePath.Text;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StoragePath.Text = dlg.SelectedPath;
            }
        }

        private void Open(object sender, RoutedEventArgs e)
        {
            try
            {
                log.Info("Storage opening...");
                if(StoragePath.Text != "")
                    AppConfigManager.SavePathes(StoragePath.Text);
                _canceled = false;
                this.Close();
                log.Info("Storage selection dialog closed");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }


        private void PathBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(e.AddedItems.Count>0)
            StoragePath.Text = (string)e.AddedItems[0];
        }

        private void PathBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PathBox.SelectedItem != null)
            {
                _canceled = false;
                StoragePath.Text = PathBox.SelectedItem as string;
                this.Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_canceled)
                StoragePath.Text = "";
        }

        private void PathBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (PathBox != null && e.Key == Key.Enter)
            {
                _canceled = false;
                StoragePath.Text = PathBox.SelectedItem as string;
                this.Close();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string path = "";
            if(PathBox.SelectedItem!=null)
                path = PathBox.SelectedItem.ToString();
            if (path != "")
            {
                AppConfigManager.RemovePath(path);
                PathBox.ItemsSource = AppConfigManager.GetPathes();
            }
        }
    }
}
