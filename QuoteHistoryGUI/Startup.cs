﻿using log4net;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace QuoteHistoryGUI
{
    static class Startup
    {
        private const string HelpText =
            "Usage of Quote History:\n" +
            "-h[elp]                          - get help.\n" +
            "-i[mport]                        - open import dialog.\n" +
            "-i[mport] <Destination> <Source> - import storage from Source to Destination.\n\n" +
            "Example:\n" +
            "QuoteHistoryGUI.exe -import \"C:\\Quotes History\" \"C:\\New Quotes History\"";

        public static readonly ILog log = LogManager.GetLogger(typeof(Startup));
        

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        [STAThread]
        public static void Main(string[] args)
        {
            
            log4net.Config.XmlConfigurator.Configure();
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "-h":
                    case "-help":
                        ShowUsage();
                        break;
                    case "-i":
                    case "-import":
                        try
                        {
                            string Source = null;
                            string Destination = null;
                            string templates = null;
                            string types = null;
                            if (args.Length == 2 || args.Length > 5)
                            {
                                Console.Out.WriteLine("\nIncorrect arguments. See usage:");
                                ShowUsage();
                                return;
                            }

                            if (args.Length == 3)
                            {
                                Console.Out.WriteLine($"\nImporting from \"{args[2]}\" to \"{args[1]}\"");
                                Source = args[2];
                                Destination = args[1];
                            }

                            if (args.Length == 4)
                            {
                                Console.Out.WriteLine($"\nImporting from \"{args[2]}\" to \"{args[1]}\"");
                                Source = args[2];
                                Destination = args[1];
                                templates = args[3];
                            }

                            if (args.Length == 5)
                            {
                                Console.Out.WriteLine($"\nImporting from \"{args[2]}\" to \"{args[1]}\"");
                                Source = args[2];
                                Destination = args[1];
                                templates = args[3];
                                types = args[4];
                            }

                            if (!Directory.Exists(Destination + "\\HistoryDB"))
                                Directory.CreateDirectory(Destination + "\\HistoryDB");

                            var loadingMode = Models.StorageInstanceModel.LoadingMode.None;
                            if (templates != null)
                                loadingMode = Models.StorageInstanceModel.LoadingMode.Sync;
                            ConsoleCommands.Import(new Models.StorageInstanceModel(Destination, null, loadingMode: loadingMode), new Models.StorageInstanceModel(Source, null, loadingMode: loadingMode), templates, types);

                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine("Check pathes and close storages!\r\nError: " + e.Message);
                            log.Error("Check pathes and close storages!\r\nError: " + e.Message + "\r\n" + e.StackTrace);
                        }
                        break;
                    case "-u":
                    case "-upstream":
                        try
                        {

                            string Source = null;
                            string templates = "*";
                            string upstreamType = null;
                            int degree = 8;
                            if (args.Length == 2 || args.Length > 6)
                            {
                                Console.Out.WriteLine("\nIncorrect arguments. See usage:");
                                ShowUsage();
                                return;
                            }

                            if (args.Length == 2)
                            {
                                Source = args[1];
                            }

                            if (args.Length == 3)
                            {
                                Source = args[1];
                                templates = args[2];
                            }

                            if (args.Length == 4)
                            {
                                Source = args[1];
                                templates = args[2];
                                upstreamType = args[3];
                            }

                            if (args.Length == 5)
                            {
                                Console.Out.WriteLine("Upstreaming...");
                                Source = args[1];
                                templates = args[2];
                                upstreamType = args[3];
                                degree = int.Parse(args[4]);
                            }

                            var loadingMode = Models.StorageInstanceModel.LoadingMode.Sync;
                            ConsoleCommands.Upstream(new Models.StorageInstanceModel(Source, null, loadingMode: loadingMode), templates, upstreamType, degree);

                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine("Check params!\r\nError: " + e.Message);
                            log.Error("Check params!\r\nError: " + e.Message + "\r\n" + e.StackTrace);
                        }
                        break;
                    default:
                        Console.Out.WriteLine("Cannot understand params");
                        break;
                }
            }
            else {
                FreeConsole();
                new QHApp().Run(); }
        }

        private static void ShowUsage()
        {
            Console.Out.WriteLine();
            Console.Out.WriteLine(HelpText);
        }
    }
}
