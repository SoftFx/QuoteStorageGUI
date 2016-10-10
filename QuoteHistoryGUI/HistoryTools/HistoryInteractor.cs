using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryInteractor
    {
        public StorageInstance Source;
        public StorageInstance Destination;

        public List<Folder> Selection = new List<Folder>();

        public void AddToSelection(Folder fold)
        {
            Folder current = fold.Parent;
            while (current != null)
            {
                if (current.Selected == true) return;
                current = current.Parent;
            }
            if (!Selection.Contains(fold)) Selection.Add(fold);
        }

        public void DiscardSelection()
        {
            Selection.Clear();
        }
        public void Copy(BackgroundWorker worker = null)
        {
            var it = Source.HistoryStoreDB.CreateIterator();
            int copiedCnt = 0;
            foreach (var fold in Selection)
            {
                
                if (fold as ChunkFile == null && fold as MetaFile == null)
                {


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

                            while (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 1,true,true))
                            {
                                copiedCnt++;
                                if (copiedCnt % 20 == 0)
                                {
                                    worker.ReportProgress(1);
                                }
                                Destination.HistoryStoreDB.Put(it.GetKey(), it.GetValue());
                                it.Next();
                            }
                        }
                    }
                }
                else
                {
                    byte[] key = new byte[] { } ;
                    var path = HistoryDatabaseFuncs.GetPath(fold);
                    int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
                    if(fold as ChunkFile != null)
                    {
                        ChunkFile chunk = fold as ChunkFile;
                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", chunk.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], chunk.Part);
                        it.Seek(key);
                    }
                    if (fold as MetaFile != null)
                    {
                        MetaFile metaFile = fold as MetaFile;
                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                        it.Seek(key);
                    }
                    if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                    {
                        Destination.HistoryStoreDB.Put(it.GetKey(), it.GetValue());
                    }

                }
            }
            it.Dispose();
        }

        public void Delete()
        {
            var it = Source.HistoryStoreDB.CreateIterator();
            var sel = Selection.ToArray();
            HistoryEditor editor = new HistoryEditor(Source.HistoryStoreDB);
            foreach (var fold in sel)
            {
                if (fold.Parent != null)
                    fold.Parent.Folders.Remove(fold);
                else
                {
                    Source.Folders.Remove(fold);
                }
                if (fold as ChunkFile == null && fold as MetaFile == null)
                {
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

                            while (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 1,true,true))
                            {
                                Source.HistoryStoreDB.Delete(it.GetKey());
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
                    }
                    if (fold as MetaFile != null)
                    {
                        MetaFile metaFile = fold as MetaFile;
                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                        it.Seek(key);
                    }
                    if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                    {
                        fold.Parent.Folders.Remove(fold);
                        Source.HistoryStoreDB.Delete(it.GetKey());
                        fold.Parent.Folders.Clear();
                        fold.Parent.Folders.Add(new LoadingFolder());
                        var ha = new HistoryLoader(Application.Current.Dispatcher, Source.HistoryStoreDB, fold.Parent.Folders, fold.Parent);
                        ha.ReadDateTimes(fold.Parent, Source.Editor);
                    }

                }
            }
            it.Dispose();
        }



    }
}
