using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static QuoteHistoryGUI.HistoryTools.HistoryDatabaseFuncs;

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryInteractor
    {
        public StorageInstanceModel Source;
        public StorageInstanceModel Destination;
        public Dispatcher Dispatcher;
        public List<Folder> Selection = new List<Folder>();

        public HistoryInteractor(Dispatcher dispatcher = null)
        {
            Dispatcher = dispatcher;
        }


        public static IEnumerable<string> GetTemplates(IEnumerable<Folder> selection)
        {
            List<string> result = new List<string>();
            foreach (var sel in selection)
            {
                string res = "";
                string path = "";
                var curSel = sel;
                while (curSel != null)
                {
                    path = curSel.Name + "/" + path;
                    curSel = curSel.Parent;
                }
                res += path.Substring(0, path.Length - 1);
                result.Add(res);
            }
            return result;
        }

        public void AddToSelection(Folder fold)
        {
            Folder current = fold.Parent;
            while (current != null)
            {
                if (Selection.Contains(current)) return;
                current = current.Parent;
            }
            if (!Selection.Contains(fold)) { Selection.Add(fold); }
        }

        public void DiscardSelection()
        {
            Selection.Clear();
        }



        public void Copy(BackgroundWorker worker = null, IEnumerable<Folder> selection = null, List<string> periods = null, Action<string> reportAction = null, IEnumerable<KeyValuePair<string,string>> mapping = null)
        {
            List<byte[]> deleteList = new List<byte[]>();
            int copiedCnt = 0;
            DateTime lastReport = DateTime.UtcNow;


            if (selection == null)
                selection = Selection;
            foreach (var fold in selection)
            {
                var files = Source.Editor.EnumerateFilesInFolder(fold, periods);
                foreach (var file in files)
                {
                    if (worker?.CancellationPending == true)
                    {
                        return;
                    }
                    if (mapping == null)
                    {
                        Destination.HistoryStoreDB.Put(file.Key, file.Value);
                    }
                    else
                    {
                        var dbentry = DeserealizeKey(file.Key);
                        var currentMapping = mapping.Where(t => t.Key == dbentry.Symbol);
                        foreach(var map in currentMapping)
                        {
                            Destination.HistoryStoreDB.Put(SerealizeKey(map.Value,dbentry.Type,dbentry.Period,dbentry.Time.Year, dbentry.Time.Month, dbentry.Time.Day, dbentry.Time.Hour, dbentry.Part, dbentry.FlushPart), file.Value);
                        }
                    }
                    copiedCnt++;

                    if (reportAction != null && (DateTime.UtcNow - lastReport).Seconds > 0.25)
                    {
                        var dbentry = DeserealizeKey(file.Key);
                        reportAction.Invoke("[" + copiedCnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                        lastReport = DateTime.UtcNow;
                    }
                }
            }
            
            reportAction.Invoke("[" + copiedCnt + "] files copied");
        }

        public void NtfsExport(BackgroundWorker worker = null, IEnumerable<Folder> selection = null, string NtfsPath = "NtfsExport", List<string> periods = null, Action<string> reportAction = null)
        {
            List<byte[]> deleteList = new List<byte[]>();
            int copiedCnt = 0;
            DateTime lastReport = DateTime.UtcNow;


            if (selection == null)
                selection = Selection;
            foreach (var fold in selection)
            {
                var files = Source.Editor.EnumerateFilesInFolder(fold, periods, types:new List<string>() { "Chunk"});
                foreach (var file in files)
                {
                    if (worker?.CancellationPending == true)
                    {
                        return;
                    }
                    DBEntry dbentry = DeserealizeKey(file.Key);
                    string fileFormat = (file.Value.Length > 2 && file.Value[0] == 'P' && file.Value[1] == 'K') ? ".zip" : ".txt";
                    string fileName = dbentry.Symbol + " " + dbentry.Period + " " + dbentry.Time.ToString("yyyy-MM-dd hh") + (dbentry.Part == 0 ? "" : "."+dbentry.Part.ToString());

                    if (!Directory.Exists(NtfsPath))
                        Directory.CreateDirectory(NtfsPath);

                    File.WriteAllBytes(NtfsPath+"/"+fileName + fileFormat, file.Value);
                    copiedCnt++;

                    if (reportAction != null && (DateTime.UtcNow - lastReport).Seconds > 0.25)
                    {
                        dbentry = DeserealizeKey(file.Key);
                        reportAction.Invoke("[" + copiedCnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                        lastReport = DateTime.UtcNow;
                    }
                }
            }

            reportAction.Invoke("[" + copiedCnt + "] files copied");
        }

        public void Copy(IEnumerable<DBEntry> matchedEntries, BackgroundWorker worker = null, bool copyChunk = false)
        {

            int copiedCnt = 0;
            DateTime lastReport = DateTime.UtcNow.AddSeconds(-2);

            foreach (var entry in matchedEntries)
            {
                copiedCnt++;

                if (worker != null && (DateTime.UtcNow - lastReport).Seconds > 1)
                {
                    worker.ReportProgress(1, "Copied " + copiedCnt + "items");
                    lastReport = DateTime.UtcNow;
                }
                byte[] key;
                byte[] value;
                if (copyChunk)
                {
                    int flushPart = 0;
                    key = SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part, flushPart);
                    value = Source.HistoryStoreDB.Get(key);
                    while (value != null)
                    {
                        Destination.HistoryStoreDB.Put(key, value);
                        flushPart++;
                        key = SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part, flushPart);
                        value = Source.HistoryStoreDB.Get(key);
                    }
                }
                else
                {
                    key = SerealizeKey(entry.Symbol, entry.Type, entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part, entry.FlushPart);
                    value = Source.HistoryStoreDB.Get(key);
                    if (value != null)
                        Destination.HistoryStoreDB.Put(key, value);

                }

            }

            worker.ReportProgress(1, "[" + copiedCnt + "] files copied");
        }

        public int Delete(IEnumerable<Folder> selection = null, BackgroundWorker worker = null, bool forsed = false)
        {
            int deleteCnt = 0;
            DateTime ReportTime = DateTime.UtcNow.AddSeconds(-2);
            if (selection == null)
                selection = Selection;



            HistoryEditor editor = new HistoryEditor(Source.HistoryStoreDB);
            foreach (var fold in selection)
            {
                var it = Source.HistoryStoreDB.CreateIterator();
                if (fold as ChunkFile == null && fold as MetaFile == null)
                {

                    if (fold.Parent == null)
                    {
                        if (Dispatcher != null)
                            Dispatcher.Invoke((Action)delegate () { Source.Folders.Remove(fold); });
                        else Source.Folders.Remove(fold);

                    }
                    else
                    {
                        if (Dispatcher != null)
                            Dispatcher.Invoke((Action)delegate () { fold.Parent.Folders.Remove(fold); });
                        else fold.Parent.Folders.Remove(fold);
                    }




                    var path = HistoryDatabaseFuncs.GetPath(fold);
                    int[] dateTime = { 2000, 1, 1, 0 };
                    for (int i = 1; i < path.Count; i++)
                    {
                        dateTime[i - 1] = int.Parse(path[i].Name);
                    }
                    foreach (var period in HistoryDatabaseFuncs.periodicityDict)
                    {
                        foreach (var type in HistoryDatabaseFuncs.typeDict)
                        {
                            var key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, type.Key, period.Key, dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0);
                            it.Seek(key);
                            while (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 1, true, true, false, false))
                            {
                                deleteCnt++;

                                DeleteWorkerReport(worker, ref ReportTime, ref deleteCnt, it.Key());
                                if (worker?.CancellationPending == true)
                                {
                                    it.Dispose();
                                    return 0;
                                }
                                Source.HistoryStoreDB.Delete(it.Key());
                                it.Next();
                            }
                        }
                    }
                }
                else
                {
                    byte[] key = new byte[] { };
                    var path = HistoryDatabaseFuncs.GetPath(fold);
                    int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
                    if (fold as ChunkFile != null)
                    {
                        ChunkFile chunk = fold as ChunkFile;
                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", chunk.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], chunk.Part);
                        it.Seek(key);
                        if (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 2, true, true, true))
                        {
                            if (Dispatcher != null)
                                Dispatcher.Invoke((Action)delegate () { fold.Parent.Folders.Remove(fold); });
                            else fold.Parent.Folders.Remove(fold);
                            deleteCnt++;
                            Source.HistoryStoreDB.Delete(it.Key());

                            DeleteWorkerReport(worker, ref ReportTime, ref deleteCnt, it.Key());
                            if (worker?.CancellationPending == true)
                            {
                                it.Dispose();
                                return 0;
                            }
                        }

                        it = Source.HistoryStoreDB.CreateIterator();

                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", chunk.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], chunk.Part);
                        it.Seek(key);
                        if (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 2, true, true, true))
                        {
                            foreach (var f in fold.Parent.Folders)
                            {
                                if (f as MetaFile != null)
                                {
                                    var meta = f as MetaFile;
                                    var delChunk = fold as ChunkFile;
                                    if (meta.Period == delChunk.Period && meta.Part == delChunk.Part)
                                    {
                                        if (Dispatcher != null)
                                            Dispatcher.Invoke((Action)delegate () { fold.Parent.Folders.Remove(meta); });
                                        else fold.Parent.Folders.Remove(meta);
                                        deleteCnt++;
                                        Source.HistoryStoreDB.Delete(it.Key());

                                        DeleteWorkerReport(worker, ref ReportTime, ref deleteCnt, key);
                                        break;
                                    }
                                    if (worker?.CancellationPending == true)
                                    {
                                        it.Dispose();
                                        return 0;
                                    }
                                }
                            }
                        }
                    }
                    if (fold as MetaFile != null)
                    {
                        MetaFile metaFile = fold as MetaFile;
                        if (forsed)
                        {
                            key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                            deleteCnt++;
                            Source.HistoryStoreDB.Delete(key);
                        }
                        else
                        {
                            key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                            it.Seek(key);
                            if (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 2, true, true, true))
                            {
                                MessageBox.Show("Unable to delete Meta when Chunk file exists. Delete Chunk File and Meta will be deleted too.", "Delete error", MessageBoxButton.OK, MessageBoxImage.Error);
                                it.Dispose();
                                return -1;
                            }
                            else
                            {
                                key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                                it.Seek(key);
                                if (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 2, true, true, true))
                                {
                                    fold.Parent.Folders.Remove(metaFile);
                                    deleteCnt++;
                                    Source.HistoryStoreDB.Delete(it.Key());

                                    DeleteWorkerReport(worker, ref ReportTime, ref deleteCnt, key);
                                    if (worker?.CancellationPending == true)
                                    {
                                        it.Dispose();
                                        return 0;
                                    }
                                }
                            }
                        }
                    }
                }
                it.Dispose();
            }
            if (worker != null)
                worker.ReportProgress(1, "[" + deleteCnt + "] files deleted");

            return 1;
        }

        void DeleteWorkerReport(BackgroundWorker worker, ref DateTime ReportTime, ref int deleteCnt, byte[] key)
        {
            if (worker != null && (DateTime.Now - ReportTime).Seconds > 0.25)
            {
                var dbentry = DeserealizeKey(key);
                worker.ReportProgress(1, "[" + deleteCnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                ReportTime = DateTime.Now;
            }
        }


        public void Import(bool replace = true, BackgroundWorker worker = null, Action<byte[], int> reportAction = null)
        {
            var sourceIter = Source.HistoryStoreDB.CreateIterator();
            sourceIter.SeekToFirst();
            DateTime ReportTime = DateTime.UtcNow.AddSeconds(-2);
            int cnt = 0;
            while (sourceIter.Valid())
            {
                if (worker?.CancellationPending == true)
                {
                    sourceIter.Dispose();
                    return;
                }

                cnt++;
                if (replace)
                {
                    Destination.HistoryStoreDB.Put(sourceIter.Key(), sourceIter.Value());
                }
                else
                {
                    if (Destination.HistoryStoreDB.Get(sourceIter.Key()) == null)
                    {
                        Destination.HistoryStoreDB.Put(sourceIter.Key(), sourceIter.Value());
                    }
                }

                if (reportAction != null && (DateTime.UtcNow - ReportTime).Seconds > 0.25)
                {
                    reportAction.Invoke(sourceIter.Key(), cnt);
                    ReportTime = DateTime.UtcNow;
                }

                sourceIter.Next();
            }
            sourceIter.Dispose();
        }

        public void ExportAllNtfs(bool replace = true, string NtfsPath = "NtfsExport", BackgroundWorker worker = null, Action<byte[], int> reportAction = null)
        {
            var sourceIter = Source.HistoryStoreDB.CreateIterator();
            sourceIter.SeekToFirst();
            DateTime ReportTime = DateTime.UtcNow.AddSeconds(-2);
            int cnt = 0;
            while (sourceIter.Valid())
            {
                if (worker?.CancellationPending == true)
                {
                    sourceIter.Dispose();
                    return;
                }

                cnt++;

                DBEntry dbentry = DeserealizeKey(sourceIter.Key());
                if (dbentry.Type == "Chunk")
                {
                    string fileFormat = (sourceIter.Value().Length > 2 && sourceIter.Value()[0] == 'P' && sourceIter.Value()[1] == 'K') ? ".zip" : ".txt";
                    string fileName = dbentry.Symbol + " " + dbentry.Period + " " + dbentry.Time.ToString("yyyy-MM-dd HH") + (dbentry.Part == 0 ? "" : "." + dbentry.Part.ToString());
                    if (!Directory.Exists(NtfsPath))
                        Directory.CreateDirectory(NtfsPath);
                    File.WriteAllBytes(NtfsPath + "/" + fileName + fileFormat, sourceIter.Value());
                }
                if (reportAction != null && (DateTime.UtcNow - ReportTime).Seconds > 0.25)
                {
                    reportAction.Invoke(sourceIter.Key(), cnt);
                    ReportTime = DateTime.UtcNow;
                }
                sourceIter.Next();
            }
            sourceIter.Dispose();
        }

        public void Upstream(IEnumerable<string> templates, BackgroundWorker worker, SelectTemplateWorker temW, Action<string> reportAction, int degreeOfParallelism, int upstreamType)
        {
            var templNum = 0;
            int flushCnt = 0;
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListTicks = new List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>>();
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListBids = new List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>>();
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListAsks = new List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>>();

            List<HistoryDatabaseFuncs.DBEntry> entriesForM1Update = new List<HistoryDatabaseFuncs.DBEntry>();
            List<HistoryDatabaseFuncs.DBEntry> entriesForH1Update = new List<HistoryDatabaseFuncs.DBEntry>();

            foreach (var templ in templates)
            {
                reportAction.Invoke("upstreaming by template: " + templ);
                templNum++;
                var matched = temW.GetByMatch(templ, worker);
                DateTime lastReport = DateTime.UtcNow.AddSeconds(-2);
                int upstramCnt = 0;
                if (upstreamType == 0 || upstreamType == 1)
                    foreach (var sel in matched)
                    {
                        var files = this.Source.Editor.EnumerateFilesInFolder(sel, new List<string>() { "ticks level2" }, new List<string>() { "Chunk" });
                        level2ToTicksWork(worker, files, ref upstramCnt, ref flushCnt, ref lastReport, saveListTicks, entriesForM1Update, reportAction, degreeOfParallelism);
                    }
                FlushWork(worker, saveListTicks, ref flushCnt, ref lastReport, reportAction);

                if (upstreamType == 0 || upstreamType == 2)
                    foreach (var sel in matched)
                    {
                        entriesForM1Update.Clear();
                        var files = this.Source.Editor.EnumerateFilesInFolder(sel, new List<string>() { "ticks" }, new List<string>() { "Chunk" });
                        foreach (var file in files)
                        {
                            var entry = HistoryDatabaseFuncs.DeserealizeKey(file.Key);
                            if (entriesForM1Update.Count == 0 || entriesForM1Update.Last().Time.Year != entry.Time.Year ||
                                entriesForM1Update.Last().Time.Month != entry.Time.Month || entriesForM1Update.Last().Time.Day != entry.Time.Day)
                            {
                                entriesForM1Update.Add(new HistoryDatabaseFuncs.DBEntry(entry.Symbol, new DateTime(entry.Time.Year, entry.Time.Month, entry.Time.Day), entry.Period, "chunk", 0));
                            }
                        }

                        ticksToM1Work(worker, entriesForM1Update, ref upstramCnt, ref flushCnt, ref lastReport, saveListBids, saveListAsks, reportAction);
                        entriesForM1Update.Clear();
                    }
                FlushWork(worker, saveListBids, ref flushCnt, ref lastReport, reportAction);
                FlushWork(worker, saveListAsks, ref flushCnt, ref lastReport, reportAction);

                if (upstreamType == 0 || upstreamType == 3)
                    foreach (var sel in matched)
                    {
                        var files = this.Source.Editor.EnumerateFilesInFolder(sel, new List<string>() { "M1 bid" }, new List<string>() { "Chunk" });
                        foreach (var file in files)
                        {
                            var entry = HistoryDatabaseFuncs.DeserealizeKey(file.Key);
                            if (entriesForH1Update.Count == 0 || entriesForH1Update.Last().Time.Year != entry.Time.Year ||
                                entriesForH1Update.Last().Time.Month != entry.Time.Month)
                            {
                                entriesForH1Update.Add(new HistoryDatabaseFuncs.DBEntry(entry.Symbol, new DateTime(entry.Time.Year, entry.Time.Month, 1), entry.Period, "chunk", 0));
                            }
                        }

                        M1ToH1Work(worker, entriesForH1Update, ref upstramCnt, ref flushCnt, ref lastReport, saveListBids, reportAction);
                        entriesForH1Update.Clear();

                        files = this.Source.Editor.EnumerateFilesInFolder(sel, new List<string>() { "M1 ask" }, new List<string>() { "Chunk" });
                        foreach (var file in files)
                        {
                            var entry = HistoryDatabaseFuncs.DeserealizeKey(file.Key);
                            if (entriesForH1Update.Count == 0 || entriesForH1Update.Last().Time.Year != entry.Time.Year ||
                                entriesForH1Update.Last().Time.Month != entry.Time.Month)
                            {
                                entriesForH1Update.Add(new HistoryDatabaseFuncs.DBEntry(entry.Symbol, new DateTime(entry.Time.Year, entry.Time.Month, 1), entry.Period, "chunk", 0));
                            }
                        }

                        M1ToH1Work(worker, entriesForH1Update, ref upstramCnt, ref flushCnt, ref lastReport, saveListAsks, reportAction);
                        entriesForH1Update.Clear();
                    }
                FlushWork(worker, saveListBids, ref flushCnt, ref lastReport, reportAction);
                FlushWork(worker, saveListAsks, ref flushCnt, ref lastReport, reportAction);
                reportAction.Invoke(flushCnt + " files builded by upstream");
            }
        }

        void level2ToTicksWork(BackgroundWorker worker, IEnumerable<KeyValuePair<byte[], byte[]>> files, ref int upstramCnt, ref int flushCnt, ref DateTime lastReport,
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListTicks, List<HistoryDatabaseFuncs.DBEntry> entriesForM1Update, Action<string> reportAction, int degreeOfParallelism = 4)
        {
            foreach (var file in files)
            {
                if (worker?.CancellationPending == true)
                {
                    return;
                }
                Interlocked.Increment(ref upstramCnt);
                var entry = HistoryDatabaseFuncs.DeserealizeKey(file.Key);
                if ((DateTime.UtcNow - lastReport).TotalSeconds > 0.5)
                {
                    reportAction.Invoke("[" + upstramCnt + "] " + entry.Symbol + "/" + entry.Time.Year + "/" + entry.Time.Month + "/" + entry.Time.Day + "/" + entry.Time.Hour + "/" + entry.Period + "." + entry.Part);
                    lastReport = DateTime.UtcNow;
                }

                var items = HistorySerializer.Deserialize("ticks level2", this.Source.Editor.GetOrUnzip(file.Value), degreeOfParallelism);
                var itemsList = new List<QHItem>();
                var ticksLevel2 = items as IEnumerable<QHTickLevel2>;
                var ticks = this.Source.Editor.GetTicksFromLevel2(ticksLevel2);
                var content = HistorySerializer.Serialize((IEnumerable<QHItem>)(ticks));

                entry.Period = "ticks";

                if (entriesForM1Update.Count == 0 || entriesForM1Update.Last().Time.Year != entry.Time.Year ||
                    entriesForM1Update.Last().Time.Month != entry.Time.Month || entriesForM1Update.Last().Time.Day != entry.Time.Day)
                {
                    entriesForM1Update.Add(new HistoryDatabaseFuncs.DBEntry(entry.Symbol, new DateTime(entry.Time.Year, entry.Time.Month, entry.Time.Day), entry.Period, "chunk", 0));
                }

                saveListTicks.Add(this.Source.Editor.GetChunkMetaForDB(content, entry));
                if (saveListTicks.Count > 1024)
                {
                    FlushWork(worker, saveListTicks, ref flushCnt, ref lastReport, reportAction);
                }
            }
        }

        void ticksToM1Work(BackgroundWorker worker, IEnumerable<HistoryDatabaseFuncs.DBEntry> entriesForM1Update, ref int upstramCnt, ref int flushCnt, ref DateTime lastReport,
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListBids, List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListAsks, Action<string> reportAction)
        {

            foreach (var entry in entriesForM1Update)
            {

                if ((DateTime.UtcNow - lastReport).TotalSeconds > 0.5)
                {
                    reportAction.Invoke("[" + upstramCnt + "] " + entry.Symbol + "/" + entry.Time.Year + "/" + entry.Time.Month + "/" + entry.Time.Day + "/" + entry.Time.Hour + "/" + entry.Period + "." + entry.Part);
                    lastReport = DateTime.UtcNow;
                }
                if (worker?.CancellationPending == true)
                {
                    return;
                }
                upstramCnt++;
                var file = this.Source.Editor.ReadAllPart(entry, HistoryEditor.ReadMode.ticksAllDate);
                var items = HistorySerializer.Deserialize("ticks", file);
                var itemsList = new List<QHItem>();
                var ticks = items as IEnumerable<QHTick>;
                var bars = this.Source.Editor.GetM1FromTicks(ticks);
                var contentBid = HistorySerializer.Serialize(bars.Key);
                var contentAsk = HistorySerializer.Serialize(bars.Value);
                var bidEntry = new HistoryDatabaseFuncs.DBEntry(entry.Symbol, entry.Time, "M1 bid", "Chunk", 0);
                var askEntry = new HistoryDatabaseFuncs.DBEntry(entry.Symbol, entry.Time, "M1 ask", "Chunk", 0);
                saveListBids.Add(this.Source.Editor.GetChunkMetaForDB(contentBid, bidEntry));
                saveListAsks.Add(this.Source.Editor.GetChunkMetaForDB(contentAsk, askEntry));

                if (saveListBids.Count > 1024)
                {
                    FlushWork(worker, saveListBids, ref flushCnt, ref lastReport, reportAction);
                }

                if (saveListAsks.Count > 1024)
                {
                    FlushWork(worker, saveListAsks, ref flushCnt, ref lastReport, reportAction);
                }
            }
        }

        void M1ToH1Work(BackgroundWorker worker,  IEnumerable<HistoryDatabaseFuncs.DBEntry> entriesForH1Update, ref int upstramCnt, ref int flushCnt, ref DateTime lastReport,
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveList, Action<string> reportAction)
        {

            foreach (var entry in entriesForH1Update)
            {

                if ((DateTime.UtcNow - lastReport).TotalSeconds > 0.5)
                {
                    reportAction.Invoke("[" + upstramCnt + "] " + entry.Symbol + "/" + entry.Time.Year + "/" + entry.Time.Month + "/" + entry.Time.Day + "/" + entry.Time.Hour + "/" + entry.Period + "." + entry.Part);
                    lastReport = DateTime.UtcNow;
                }
                if (worker?.CancellationPending == true)
                {
                    return;
                }
                upstramCnt++;
                var file = this.Source.Editor.ReadAllPart(entry, HistoryEditor.ReadMode.H1AllDate);
                var items = HistorySerializer.Deserialize("M1 bid", file);
                var itemsList = new List<QHItem>();
                var m1Bars = items as IEnumerable<QHBar>;
                var bars = this.Source.Editor.GetH1FromM1(m1Bars);
                var content = HistorySerializer.Serialize(bars);
                var Entry = new HistoryDatabaseFuncs.DBEntry(entry.Symbol, entry.Time, entry.Period == "M1 bid" ? "H1 bid" : "H1 ask", "Chunk", 0);
                saveList.Add(this.Source.Editor.GetChunkMetaForDB(content, Entry));

                if (saveList.Count > 1024)
                {
                    FlushWork(worker,  saveList, ref flushCnt, ref lastReport, reportAction);
                }
            }
        }

        void FlushWork(BackgroundWorker worker,  List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveList, ref int flushCnt, ref DateTime lastReport, Action<string> reportAction)
        {
            foreach (var pairForChunk in saveList)
            {
                if (worker?.CancellationPending == true)
                {
                    return;
                }
                flushCnt++;
                this.Source.HistoryStoreDB.Put(pairForChunk.Key.Key, pairForChunk.Key.Value);
                if ((DateTime.UtcNow - lastReport).TotalSeconds > 0.25)
                {
                    reportAction.Invoke("[" + flushCnt + "] " + "Flushing");
                    lastReport = DateTime.UtcNow;
                }
            }
            foreach (var pairForMeta in saveList)
            {
                if (worker?.CancellationPending == true)
                {
                    return;
                }
                flushCnt++;
                this.Source.HistoryStoreDB.Put(pairForMeta.Value.Key, pairForMeta.Value.Value);
                if ((DateTime.UtcNow - lastReport).TotalSeconds > 0.25)
                {
                    reportAction.Invoke("[" + flushCnt + "] " + "Flushing");
                    lastReport = DateTime.UtcNow;
                }
            }
            saveList.Clear();
        }
    }
}

