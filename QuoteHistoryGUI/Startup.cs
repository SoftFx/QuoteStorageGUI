using log4net;
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
        public static int Main(string[] args)
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
                    case "-e":
                    case "-export":
                    case "-i":
                    case "-import":
                        try
                        {
                            string Source = null;
                            string Destination = null;
                            string templates = null;
                            string types = null;
                            if (args.Length == 2)
                            {
                                Console.Out.WriteLine("\nIncorrect arguments. See usage:");
                                ShowUsage();
                                return -1;
                            }

                            Source = args[2];
                            Destination = args[1];

                            var paramDict = ConsoleCommands.ParseOptions(ConsoleCommands.ImportParamsDict, args, 3);

                            if (args[0] == "-e" || args[0] == "-export")
                            {
                                var buf = Source;
                                Source = Destination;
                                Destination = buf;
                            }

                            return ConsoleCommands.Copy(Source, Destination, paramDict["-templates"], paramDict["-type"], paramDict["-format"]);
                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine("Check pathes and close storages!\r\nError: " + e.Message);
                            log.Error("Check pathes and close storages!\r\nError: " + e.Message + "\r\n" + e.StackTrace);
                            return -1;
                        }
                        break;
                    case "-u":
                    case "-upstream":
                        try
                        {
                            if (args.Length == 1)
                            {
                                Console.Out.WriteLine("\nIncorrect arguments. See usage:");
                                ShowUsage();
                                return -1;
                            }
                            string Source = args[1];

                            var paramDict = ConsoleCommands.ParseOptions(ConsoleCommands.UpstreamParamsDict, args, 2);
                            
                            var loadingMode = Models.StorageInstanceModel.LoadingMode.Sync;

                            return ConsoleCommands.Upstream(new Models.StorageInstanceModel(Source, null, loadingMode: loadingMode), paramDict["-templates"], paramDict["-type"], int.Parse(paramDict["-degree"]));

                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine("An error has occurred.\r\nError: " + e.Message);
                            log.Error("An error has occurred.\r\nError: " + e.Message + "\r\n" + e.StackTrace);
                            return -1;
                        }
                        break;
                    default:
                        Console.Out.WriteLine("Cannot understand params");
                        break;
                }
            }
            else
            {
                FreeConsole();
                new QHApp().Run();
            }
            return 0;
        }

        private static void ShowUsage()
        {
            Console.Out.WriteLine();
            Console.Out.WriteLine(HelpText);
        }
    }
}
