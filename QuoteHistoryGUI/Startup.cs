using log4net;
using QuoteHistoryGUI.Views;
using System;
using System.Collections.Generic;
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


        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            AttachConsole(ATTACH_PARENT_PROCESS);

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
                            QHApp myConsoleApp = new QHApp {ApplicationMode = QHApp.AppMode.ImportDialog};

                            if (args.Length == 2 || args.Length > 4)
                            {
                                Console.Out.WriteLine("\nIncorrect arguments. See usage:");
                                ShowUsage();
                                return;
                            }

                            if (args.Length == 3)
                            {
                                Console.Out.WriteLine($"\nImporting from \"{args[2]}\" to \"{args[1]}\"");
                                myConsoleApp.ApplicationMode = QHApp.AppMode.Console;
                                myConsoleApp.Source = args[2];
                                myConsoleApp.Destination = args[1];
                            }

                            if (args.Length == 4)
                            {
                                Console.Out.WriteLine($"\nImporting from \"{args[2]}\" to \"{args[1]}\"");
                                myConsoleApp.ApplicationMode = QHApp.AppMode.Console;
                                myConsoleApp.Source = args[2];
                                myConsoleApp.Destination = args[1];
                                myConsoleApp.templates = args[3];
                            }

                            myConsoleApp.Run();
                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine("Check pathes and close storages!\r\nError: " + e.Message);
                            log.Error("Check pathes and close storages!\r\nError: "+e.Message+"\r\n"+e.StackTrace);
                        }
                        break;
                    default:
                        new QHApp().Run();
                        break;
                }
            }
            else new QHApp().Run();
        }

        private static void ShowUsage()
        {
            Console.Out.WriteLine();
            Console.Out.WriteLine(HelpText);
        }
    }
}
