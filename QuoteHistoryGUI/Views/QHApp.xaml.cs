using QuoteHistoryGUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace QuoteHistoryGUI.Views
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class QHApp : Application
    {
        public enum AppMode
        {
            FullGUI,
            ImportDialog,
            Console
        }
        public AppMode ApplicationMode = AppMode.FullGUI;

        public string Source;
        public string Destination;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = Environment.GetCommandLineArgs();

            switch (ApplicationMode) {
                case AppMode.ImportDialog:
                    new ImportDialog().Show();
                    break;
                case AppMode.Console:
                    new ImportDialog(new Models.StorageInstanceModel(Destination), new Models.StorageInstanceModel(Source)).DoImport(false);
                    break;
                default:
                    new QHAppWindowView().ShowDialog();
                    break;         
        }
        }
    }
}
