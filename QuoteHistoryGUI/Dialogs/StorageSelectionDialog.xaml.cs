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

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for StorageSelectionDialog.xaml
    /// </summary>
    public partial class StorageSelectionDialog : Window
    {
        private int _pathCount = 0;
        List<string> _pathList;

        public StorageSelectionDialog()
        {
            InitializeComponent();
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings["path_count"] == null)
                config.AppSettings.Settings.Add(new KeyValueConfigurationElement("path_count", "0"));
            _pathCount = int.Parse(config.AppSettings.Settings["path_count"].Value);
            _pathList = new List<string>();
            for(int i =0; i< _pathCount;i++)
            {
                _pathList.Add(config.AppSettings.Settings["path_" + i].Value);
            }
            config.Save();
            PathBox.ItemsSource = _pathList;
        }

        private void Browse(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                //SelectedPath = (string)RegistryFunctions.GetRegValue(null, "UpdateOpenDbPath", "")
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StoragePath.Text = dlg.SelectedPath;
            }
        }

        private void Open(object sender, RoutedEventArgs e)
        {
            _pathList.Add(StoragePath.Text);
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            _pathCount++;
            for (int i = 0; i < _pathCount; i++)
            {
                if (config.AppSettings.Settings["path_" + i] == null)
                    config.AppSettings.Settings.Add(new KeyValueConfigurationElement("path_" + i, _pathList[i]));
                else
                    config.AppSettings.Settings["path_" + i].Value = _pathList[i];

            }
            config.AppSettings.Settings["path_count"].Value = _pathCount.ToString();
            config.Save();


            this.Close();
        }


        private void PathBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StoragePath.Text = (string)e.AddedItems[0];
        }
    }
}
