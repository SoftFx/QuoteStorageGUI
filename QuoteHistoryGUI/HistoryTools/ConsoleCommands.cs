using log4net;
using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace QuoteHistoryGUI.HistoryTools
{
    class ConsoleCommands
    {

        public static Dictionary<string, string> ParseOptions(Dictionary<string, string> defaultDict, string[] argv, int startInd = 0)
        {
            var resDict = new Dictionary<string, string>(defaultDict);
            for (int i = startInd; i < argv.Length;)
            {
                try
                {
                    resDict[argv[i]] = argv[i + 1];
                    i += 2;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Error with parsing argument " + argv[i], ex);
                }
            }
            return resDict;
        }

        public static readonly ILog log = LogManager.GetLogger(typeof(Startup));

        static Dictionary<string, int> copyTypes = new Dictionary<string, int>
        {
            { "",0 },
            { "all",0 },
            { "full",0 },
            { "level2",1 },
            { "l2",1 },
            { "ticks",2 },
            { "t",2 },
            { "m1",3 },
            { "h1",4 }
        };

        public static readonly Dictionary<string, string> ImportParamsDict = new Dictionary<string, string>
        {
            {"-templates", null},
            {"-type", null},
            {"-format", "LevelDB" }
        };

        public static int Copy(string sourcePath, string destinationPath, string templateStr = null, string typeStr = null, string format = "LevelDB")
        {
            var loadingMode = Models.StorageInstanceModel.LoadingMode.None;
            if (templateStr != null || typeStr !=null)
                loadingMode = Models.StorageInstanceModel.LoadingMode.Sync;

            var source = new Models.StorageInstanceModel(sourcePath, null, loadingMode: loadingMode);

            if (typeStr != null && templateStr == null)
                templateStr = "*";

            try
            {
                var Interactor = new HistoryInteractor();
                Interactor.Source = source;
                Console.WriteLine(DateTime.UtcNow + ": copying starting...");
                if (templateStr == null)
                {
                    
                    if (format == "LevelDB")
                    {
                        if (!Directory.Exists(destinationPath + "\\HistoryDB"))
                            Directory.CreateDirectory(destinationPath + "\\HistoryDB");
                        var destination = new Models.StorageInstanceModel(destinationPath, null, loadingMode: loadingMode);
                        Interactor.Destination = destination;
                        Interactor.Import(true, null, (key, cnt) =>
                        {
                            var dbentry = HistoryDatabaseFuncs.DeserealizeKey(key);
                            Console.WriteLine(DateTime.UtcNow + ": copying [" + cnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                        });
                        Interactor.Destination.HistoryStoreDB.Dispose();
                    }
                    else
                    {
                        Interactor.ExportAllNtfs(true, destinationPath, null, (key, cnt) =>
                         {
                             var dbentry = HistoryDatabaseFuncs.DeserealizeKey(key);
                             Console.WriteLine(DateTime.UtcNow + ": copying [" + cnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                         });
                    }
                }
                else
                {
                    int type = 0;
                    try
                    {
                        if (typeStr != null)
                            type = copyTypes[typeStr.ToLower()];
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Invalid copy type case");
                        Console.WriteLine("Possible, Case-insensitive:");
                        foreach (var key in copyTypes.Keys)
                            Console.WriteLine(key);
                        return -1;
                    }
                    List<string> types = null;
                    switch (type)
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
                    if (format == "LevelDB")
                    {
                        if (!Directory.Exists(destinationPath + "\\HistoryDB"))
                            Directory.CreateDirectory(destinationPath + "\\HistoryDB");
                        var destination = new Models.StorageInstanceModel(destinationPath, null, loadingMode: loadingMode);
                        Interactor.Destination = destination;
                        foreach (var templ in templates)
                        {

                            var matched = temW.GetByMatch(templ, null);
                            //var t = matched.ToArray();
                            Console.WriteLine(DateTime.UtcNow + ": copying with template: " + templ);
                            Interactor.Copy(null, matched, types, (message) =>
                            {
                                Console.WriteLine(DateTime.UtcNow + ": copying " + message);
                            });
                        }
                        Interactor.Destination.HistoryStoreDB.Dispose();
                    }
                    else
                    {
                        foreach (var templ in templates)
                        {

                            var matched = temW.GetByMatch(templ, null);
                            //var t = matched.ToArray();

                            Console.WriteLine(DateTime.UtcNow + ": copying with template: " + templ);

                            Interactor.NtfsExport(null, matched, destinationPath, types, (message) =>
                            {
                                Console.WriteLine(DateTime.UtcNow + ": copying " + message);
                            });
                        }
                    }

                }
                Console.WriteLine(DateTime.UtcNow + ": copying performed!");
                Interactor.Source.HistoryStoreDB.Dispose();
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return -1;
            }
            return 0;
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

        public static readonly Dictionary<string, string> UpstreamParamsDict = new Dictionary<string, string>
        {
            {"-templates", "*"},
            {"-type", ""},
            {"-degree", "8" }
        };

        public static int Upstream(StorageInstanceModel source, string templateStr = null, string upstreamType = "", int degeree = 8)
        {
            try
            {
                var Interactor = new HistoryInteractor();
                Interactor.Source = source;


                Console.Out.WriteLine(DateTime.UtcNow + ": upstream starting...");
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
                try
                {
                    upsType = upstreamTypes[upstreamType.ToLower()];
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Invalid usptream type case");
                    Console.WriteLine("Possible, Case-insensitive:");
                    foreach (var key in upstreamTypes.Keys)
                        Console.WriteLine(key);
                    return -1;
                }

                Interactor.Upstream(templates, null, temW, t => { Console.WriteLine(DateTime.UtcNow + ": upstreaming " + t); }, degreeOfParallelism, upsType);
                Interactor.Source.HistoryStoreDB.Dispose();




                Console.WriteLine(DateTime.UtcNow + ": upstream performed!");
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return -1;
            }
            return 0;
        }
    }
}
