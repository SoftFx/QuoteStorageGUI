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
    class Startup
    {

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        public static void Main(string[] args)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "-h":
                    case "-help":
                        Console.Out.WriteLine("\n");
                        Console.Out.WriteLine("Quote History GUI");
                        Console.Out.WriteLine(" ");
                        Console.Out.WriteLine("-h or -help \t get help");
                        Console.Out.WriteLine("-i or -import \t open import dialog");
                        Console.Out.WriteLine("<-i or -import> <Destination> <Source>  \t import storage");
                        break;
                    case "-i":
                    case "-import":
                        try
                        {
                            QHApp myConsoleApp = new QHApp();
                            myConsoleApp.ApplicationMode = QHApp.AppMode.ImportDialog;
                            if (args.Count() == 3)
                            {
                                Console.Out.WriteLine("");
                                Console.Out.WriteLine("Importing from " + args[2] + " to " + args[1]);
                                myConsoleApp.ApplicationMode = QHApp.AppMode.Console;
                                myConsoleApp.Source = args[2];
                                myConsoleApp.Destination = args[1];
                            }
                            myConsoleApp.Run();
                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine(e.Message);
                        }
                        break;
                    default:
                        new QHApp().Run();
                        break;
                }
            }
            else new QHApp().Run();
        }
    }
}
