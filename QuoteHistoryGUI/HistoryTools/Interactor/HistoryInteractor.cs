using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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



        public void Copy(BackgroundWorker worker = null, IEnumerable<Folder> selection = null, List<string> periods = null, Action<string> reportAction = null)
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
                    Destination.HistoryStoreDB.Put(file.Key, file.Value);
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


    }
}
