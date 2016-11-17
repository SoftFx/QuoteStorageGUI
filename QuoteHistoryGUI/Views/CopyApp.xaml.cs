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
    public partial class CopyApp : Application
    {
        bool IsUIVersion = true;
        ImportDialog dialog;
        
        public void SetParamsForConsole(string Destination, string Source)
        {
            dialog = new ImportDialog(new Models.StorageInstance(Destination), new Models.StorageInstance(Source));
            IsUIVersion = false;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = Environment.GetCommandLineArgs();

            if (args.Count() > 1 && (args[1] == "-i" || args[1] == "-import"))
            {
                if (IsUIVersion)
                {
                    dialog.ShowDialog();
                    if (dialog == null)
                        dialog = new ImportDialog();
                }
                else
                    dialog.DoImport(false);
            }
            else
                new MainWindowView().ShowDialog();
            
        }
    }
}
