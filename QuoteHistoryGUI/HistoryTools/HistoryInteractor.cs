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
                if (fold as ChunkFile == null && fold as MetaFile == null)
                {
                    if (fold.Parent == null)
                    {
                        Source.Folders.Remove(fold);
                    }
                    else
                    {
                        fold.Parent.Folders.Remove(fold);
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
                        if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                        {
                            fold.Parent.Folders.Remove(fold);
                            Source.HistoryStoreDB.Delete(it.GetKey());
                        }

                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", chunk.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], chunk.Part);
                        it.Seek(key);
                        if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                        {
                            foreach (var f in fold.Parent.Folders)
                            {
                                if (f as MetaFile != null)
                                {
                                    var meta = f as MetaFile;
                                    var delChunk = fold as ChunkFile;
                                    if (meta.Period == delChunk.Period && meta.Part == delChunk.Part)
                                    {
                                        fold.Parent.Folders.Remove(meta);
                                        Source.HistoryStoreDB.Delete(it.GetKey());
                                        break;
                                    }
                                }
                            }
                            Source.HistoryStoreDB.Delete(it.GetKey());
                        }

                    }
                    if (fold as MetaFile != null)
                    {
                        MetaFile metaFile = fold as MetaFile;

                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                        it.Seek(key);
                        if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                        {
                            if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                            {
                                MessageBox.Show("Unable to delete Meta when Chunk file exists. Delete Chunk File and Meta will be deleted too.", "Delete error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                            it.Seek(key);
                            if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                            {
                                fold.Parent.Folders.Remove(metaFile);
                                Source.HistoryStoreDB.Delete(it.GetKey());
                            }
                        }
                    }

                }
            }
            it.Dispose();
        }


        public void Import(bool replace = true, BackgroundWorker worker = null)
        {
            var sourceIter = Source.HistoryStoreDB.CreateIterator();
            sourceIter.SeekToFirst();
            DateTime ReportTime = DateTime.Now;
            while (sourceIter.IsValid())
            {
                if (replace)
                {
                    Destination.HistoryStoreDB.Put(sourceIter.GetKey(), sourceIter.GetValue());
                }
                else
                {
                    if (Destination.HistoryStoreDB.Get(sourceIter.GetKey()) == null)
                    {
                        Destination.HistoryStoreDB.Put(sourceIter.GetKey(), sourceIter.GetValue());
                    }
                }

                if(worker!=null && (DateTime.Now - ReportTime).Seconds > 1)
                {
                    worker.ReportProgress(1, sourceIter.GetKey());
                    ReportTime = DateTime.Now;
                }

                sourceIter.Next();
            }
        }

    }
}
