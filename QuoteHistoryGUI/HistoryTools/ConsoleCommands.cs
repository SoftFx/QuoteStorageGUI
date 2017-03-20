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
        public static void Import(StorageInstanceModel destination, StorageInstanceModel source, string templateStr = null) {
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
                        Interactor.Copy(null, matched, null, (message) => {
                            Console.WriteLine(DateTime.UtcNow + ": importing "+ message);
                        });
                    }

                }
                Console.WriteLine(DateTime.UtcNow + ": Import performed!");
            }
            catch(Exception ex)
            {
                Console.Write(ex);
            }
        }
    }
}
