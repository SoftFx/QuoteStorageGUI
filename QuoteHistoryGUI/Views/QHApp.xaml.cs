﻿using log4net;
using QuoteHistoryGUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
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
        public string templates;
        public static readonly ILog log = LogManager.GetLogger(typeof(QHApp));
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = Environment.GetCommandLineArgs();
            try
            {
                switch (ApplicationMode)
                {
                    case AppMode.ImportDialog:
                        new ImportDialog().Show();
                        break;
                    case AppMode.Console:
                        if (!Directory.Exists(Destination + "\\HistoryDB"))
                            Directory.CreateDirectory(Destination + "\\HistoryDB");
                        if (templates == null)
                        {
                            new ImportDialog(new Models.StorageInstanceModel(Destination, this.Dispatcher), new Models.StorageInstanceModel(Source, this.Dispatcher)).DoImport(false);
                        }
                        else
                        {
                            var dlg = new SmartImportDialog();
                            dlg.DoConsoleImport(new Models.StorageInstanceModel(Source, this.Dispatcher, loadingMode:Models.StorageInstanceModel.LoadingMode.Sync), new Models.StorageInstanceModel(Destination, this.Dispatcher,loadingMode:Models.StorageInstanceModel.LoadingMode.Sync), templates);
                        }
                        break;
                    default:

                        new QHAppWindowView().ShowDialog();
                        break;
                }
            }
            catch (Exception ex)
            {
                if (ApplicationMode == AppMode.Console)
                    Console.Write(ex.Message + ",\nStack trace: " + ex.StackTrace);
                log.Error(ex.Message + ",\nStack trace: "+ex.StackTrace);
            }
        }
    }
}
