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
                if (args.Count() == 1)
                {
                    
                    //Console.Out.WriteLine("Quote History GUI");
                    if (args[0] == "-help" || args[0] == "-h")
                    {
                        Console.Out.WriteLine("\n");
                        Console.Out.WriteLine("Quote History GUI");
                        Console.Out.WriteLine(" ");
                        Console.Out.WriteLine("-h or -help \t get help");
                        Console.Out.WriteLine("-i or -import \t open import dialog");
                        Console.Out.WriteLine("<-i or -import> <Destination> <Source>  \t import storage");
                    }
                    if (args[0] == "-import" || args[0] == "-i")
                    {
                        CopyApp myApp = new CopyApp();
                        myApp.Run();
                    }
                }
                if (args.Count() == 3)
                {

                    //Console.Out.WriteLine("Quote History GUI");
                    if (args[0] == "-help" || args[0] == "-h")
                    {
                        Console.Out.WriteLine("\n");
                        Console.Out.WriteLine("Quote History GUI");
                        Console.Out.WriteLine(" ");
                        Console.Out.WriteLine("-h or -help \t get help");
                        Console.Out.WriteLine("-i or -import \t open import dialog");
                        Console.Out.WriteLine("-i or -import \t <SOURCE> <DESTINATION>");
                    }
                    if (args[0] == "-import" || args[0] == "-i")
                    {
                        try
                        {
                            CopyApp myConsoleApp = new CopyApp();
                            Console.Out.WriteLine("");
                            Console.Out.WriteLine("Importing from " + args[2] + " to " + args[1]);
                            myConsoleApp.SetParamsForConsole(args[1], args[2]);
                            myConsoleApp.Run();
                        }
                        catch (Exception e) {
                            Console.Out.WriteLine(e.Message);
                        }
                    }
                }
            }
            else
            {
                CopyApp myApp = new CopyApp();
                myApp.Run();
            }  
        }
    }
}
