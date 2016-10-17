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
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = Environment.GetCommandLineArgs();
            if (args.Count() == 2 && (args[1] == "-i" || args[1] == "-import"))
            new ImportDialog(null, null).ShowDialog();
            else
                new MainWindowView().ShowDialog();
            
            
        }
    }
}
