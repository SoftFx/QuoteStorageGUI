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
        private int _pathCount = 0;
        List<string> _pathList;
        bool _canceled = true;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        private void SavePathes()
        {
            try
            {
                log.Info("Saving storage pathes...");
                if (!_pathList.Contains(StoragePath.Text))
                {
                    _pathList.Add(StoragePath.Text);
                    _pathCount++;
                }
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                for (int i = 0; i < _pathCount; i++)
                {
                    if (config.AppSettings.Settings["path_" + i] == null)
                        config.AppSettings.Settings.Add(new KeyValueConfigurationElement("path_" + i, _pathList[i]));
                    else
                        config.AppSettings.Settings["path_" + i].Value = _pathList[i];

                }
                config.AppSettings.Settings["path_count"].Value = _pathCount.ToString();
                config.Save();
                log.Info("Storage pathes saved");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }
        public StorageSelectionDialog()
        {
            try
            {
                log.Info("Storage selection dialog initializing");
                InitializeComponent();
                this.Closing += Window_Closing;
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["path_count"] == null)
                    config.AppSettings.Settings.Add(new KeyValueConfigurationElement("path_count", "0"));
                _pathCount = int.Parse(config.AppSettings.Settings["path_count"].Value);
                _pathList = new List<string>();
                for (int i = 0; i < _pathCount; i++)
                {
                    _pathList.Add(config.AppSettings.Settings["path_" + i].Value);
                }
                config.Save();
                PathBox.ItemsSource = _pathList;
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
                SavePathes();
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
            StoragePath.Text = (string)e.AddedItems[0];
        }

        private void PathBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PathBox.SelectedItem != null)
            {
                SavePathes();
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
                SavePathes();
                _canceled = false;
                StoragePath.Text = PathBox.SelectedItem as string;
                this.Close();
            }
        }

    }
}
