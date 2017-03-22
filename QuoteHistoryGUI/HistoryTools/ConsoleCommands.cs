using log4net;
using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace QuoteHistoryGUI.HistoryTools
{
    class ConsoleCommands
    {

        public static readonly ILog log = LogManager.GetLogger(typeof(Startup));
        public static void Import(StorageInstanceModel destination, StorageInstanceModel source, string templateStr = null, string typeStr = null) {
            try {
                var Interactor = new HistoryInteractor();
                Interactor.Destination = destination;
                Interactor.Source = source;

                if (templateStr == null)
                {
                    Console.WriteLine(DateTime.UtcNow + ": Import starting...");
                    Interactor.Import(true, null, (key, cnt) =>
                    {
                        var dbentry = HistoryDatabaseFuncs.DeserealizeKey(key);
                        Console.WriteLine(DateTime.UtcNow + ": importing [" + cnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                    });
                }
                else
                {
                    int type = 0;

                    if (typeStr != null)
                        type = upstreamTypes[typeStr.ToLower()];

                    List<string> types = null;
                    switch(type)
                    {
                        case 1:
                            types = new List<string>() { "ticks level2" };
                            break;
                        case 2:
                            types = new List<string>() { "ticks" };
                            break;
                        case 3:
                            types = new List<string>() { "M1 ask", "M1 bid" };
                            break;
                        case 4:
                            types = new List<string>() { "H1 ask", "H1 bid" };
                            break;
                        default: break;
                    }


                    StringBuilder templText = new StringBuilder();
                    foreach (var ch in templateStr)
                    {
                        templText.Append(ch);
                        if (ch == ';')
                            templText.Append('\n');
                    }

                    var templateText = templText.ToString();

                    var temW = new SelectTemplateWorker(Interactor.Source.Folders, new HistoryLoader(null, Interactor.Source.HistoryStoreDB));

                    var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);

                    foreach (var templ in templates)
                    {

                        var matched = temW.GetByMatch(templ, null);
                        //var t = matched.ToArray();
                        Interactor.Copy(null, matched, types, (message) => {
                            Console.WriteLine(DateTime.UtcNow + ": importing "+ message);
                        });
                    }
                    Interactor.Source.HistoryStoreDB.Dispose();
                    Interactor.Destination.HistoryStoreDB.Dispose();

                }
                Console.WriteLine(DateTime.UtcNow + ": Import performed!");
            }
            catch(Exception ex)
            {
                Console.Write(ex);
            }
        }

        static Dictionary<string, int> upstreamTypes = new Dictionary<string, int>
        {
            { "",0 },
            { "all",0 },
            { "full",0 },
            { "level2",1 },
            { "level2->ticks",1 },
            { "l2",1 },
            { "ticks",2 },
            { "ticks->m1",2 },
            { "t",2 },
            { "m1",3 },
            { "m1->h1",3 },
            { "h1",4 }
        };


        public static void Upstream(StorageInstanceModel source, string templateStr = null, string upstreamType = null, int degeree = 8)
        {
            try
            {
                var Interactor = new HistoryInteractor();
                Interactor.Source = source;

                try
                {
                    Console.Out.WriteLine(DateTime.UtcNow + ": Upstream starting...");
                    if (templateStr == null)
                        templateStr = "*";
                    StringBuilder templText = new StringBuilder();
                    foreach (var ch in templateStr)
                    {
                        templText.Append(ch);
                        if (ch == ';')
                            templText.Append('\n');
                    }

                    var templateText = templText.ToString();

                    var temW = new SelectTemplateWorker(Interactor.Source.Folders, new HistoryLoader(null, Interactor.Source.HistoryStoreDB));

                    var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);
                    int upsType = 0;
                    int degreeOfParallelism = degeree;
                    try {
                        upsType = upstreamTypes[upstreamType.ToLower()];
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Invalid usptream type case");
                        Console.WriteLine("Possible, Case-insensitive:");
                        foreach (var key in upstreamTypes.Keys)
                            Console.WriteLine(key);
                        return;
                    }

                    Interactor.Upstream(templates, null, temW, t => { Console.WriteLine(DateTime.UtcNow + ": upstreaming " + t); }, degreeOfParallelism, upsType);
                    Interactor.Source.HistoryStoreDB.Dispose();

                }
                catch (Exception ex)
                {
                        Console.WriteLine(ex.Message + ",\nStackTrace: " + ex.StackTrace);
                }


                Console.WriteLine(DateTime.UtcNow + ": Upstream performed!");
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }
    }
}
