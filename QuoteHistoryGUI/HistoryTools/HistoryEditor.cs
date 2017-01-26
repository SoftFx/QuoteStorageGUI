using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Checksums;
using LevelDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows;
using System.Diagnostics;

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryEditor
    {

        public static int MaxCountPerChunk = 131072;

        private DB _dbase;
        public HistoryEditor(DB db)
        {
            _dbase = db;
        }

        public byte[] GetOrUnzip(byte[] content)
        {
            if (content == null || content.Count() == 0) return new byte[] { };
            bool isZip = false;
            if (content[0] == 'P' && content[1] == 'K')
                isZip = true;
            if (!isZip)
                return content;
            else
            {
                MemoryStream data = new MemoryStream(content);
                ZipFile zip = new ZipFile(data);

                foreach (ZipEntry zipEntry in zip)
                {
                    Stream zipStream = zip.GetInputStream(zipEntry);
                    byte[] buffer = new byte[4 * 1024];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        content = ms.ToArray();
                    }
                }
                return content;
            }
        }
        public KeyValuePair<string,byte[]> ReadFromDB(HistoryFile f) {

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);

            string period = f.Period;
            byte[] content = { };
            bool isZip = false;
            if (f as ChunkFile != null)
            {
                var cnt = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                if (cnt[0] == 'P' && cnt[1] == 'K')
                    isZip = true;
                if (!isZip)
                {
                    List<byte> res = new List<byte>(cnt);
                    int flushPart = 1;
                    while (true)
                    {
                        var cntPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part, flushPart));
                        if (cntPart == null) break;
                        res.AddRange(cntPart);
                        flushPart++;
                    }
                    cnt = res.ToArray();
                }
                var Text = GetOrUnzip(cnt);
                return new KeyValuePair<string, byte[]>(isZip ? "Zip" : "Text", Text);
            }
            else
            {
                var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                // (BitConverter.ToUInt32(dbVal, 0));
                Crc32 hash = new Crc32();
                hash.Value = (BitConverter.ToUInt32(meta, 0));
                var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                contStr += '\t';
                if (meta[4] == 1)
                    contStr += "Zip";
                if (meta[4] == 2)
                    contStr += "Text";
                return new KeyValuePair<string, byte[]>("Meta", ASCIIEncoding.ASCII.GetBytes(contStr));

            }
            
        }
        public enum hourReadMode
        {
            allDate,
            oneDate
        }
        

        public byte[] ReadAllPart(HistoryFile f, hourReadMode hm = hourReadMode.oneDate)
        {

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);

            List<byte> result = new List<byte>();
            int hstart;
            int hend;
            if(hm == hourReadMode.oneDate)
            {
                hstart = dateTime[3];
                hend = dateTime[3] + 1;
            }
            else
            {
                hstart = 0;
                hend = 24;
            }
            for (int hour = hstart; hour < hend; hour++)
            {
                for (int i = 0; i < 30; i++)
                {
                    var cntnt = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", f.Period, dateTime[0], dateTime[1], dateTime[2], hour, i));
                    if (cntnt != null)
                        result.AddRange(GetOrUnzip(cntnt));
                    else break;
                }
            }
            return result.ToArray();
        }

        public byte[] ReadAllPart(HistoryDatabaseFuncs.DBEntry entry, hourReadMode hm = hourReadMode.oneDate)
        {
            
            List<byte> result = new List<byte>();
            int hstart;
            int hend;
            if (hm == hourReadMode.oneDate)
            {
                hstart = entry.Time.Hour;
                hend = entry.Time.Hour + 1;
            }
            else
            {
                hstart = 0;
                hend = 24;
            }
            for (int hour = hstart; hour < hend; hour++)
            {
                for (int i = 0; i < 30; i++)
                {
                    var cntnt = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, hour, i));
                    if (cntnt != null)
                        result.AddRange(GetOrUnzip(cntnt));
                    else break;
                }
            }
            return result.ToArray();
        }

        public int SaveToDBParted(IEnumerable<QHItem> items, ChunkFile file, bool rebuildMeta = true, bool showMessages = true)
        {
            ChunkFile f = new ChunkFile(file.Name,file.Period, 0, file.Parent);
            int part = 0;
            List<QHItem> chunk = new List<QHItem>();
            foreach(var item in items)
            {
                if (chunk.Count == MaxCountPerChunk)
                {
                    f.Part = part;
                    part++;
                    SaveToDB(HistorySerializer.Serialize(chunk), f, showMessages);
                    chunk = new List<QHItem>();
                }
                chunk.Add(item);
            }
            if (chunk.Count != 0) SaveToDB(HistorySerializer.Serialize(chunk), f, showMessages); else part--;
            return part;
        }

        public int CalculatePartCount(IEnumerable<QHItem> items)
        {
            return items.Count()/MaxCountPerChunk+ items.Count()%MaxCountPerChunk>0?1:0;
        }

        public void SaveToDB(byte[] content, ChunkFile f, bool showMessages = true)
        {
            try
            {
                HistorySerializer.Deserialize(f.Period, content);
            }
            catch(InvalidDataException ex)
            {
                MessageBox.Show("There is a syntax error! Unable to save.\n\n"+ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch
            {
                
                MessageBox.Show("There is a syntax error! Unable to save.\n\n"+ "Check the numeric format and punctuation", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.MainWindow.Activate();
                return;
            }

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            string period = f.Period;
            var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));

            var key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part);
            byte[] value = { };
            if (meta!=null && meta[4] == 2)
            {
                value = content;   
            }
            else
            {
                MemoryStream contentMemStream = new MemoryStream(content);
                MemoryStream outputMemStream = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

                zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

                ZipEntry newEntry = new ZipEntry(f.Period+".txt");
                newEntry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(newEntry);

                StreamUtils.Copy(contentMemStream, zipStream, new byte[4096]);
                zipStream.CloseEntry();

                zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
                zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

                outputMemStream.Position = 0;
                value = outputMemStream.ToArray(); 
            }

            _dbase.Put(key, value);

            RebuildMeta(f, showMessages);
        }

        public KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>> GetChunkMetaForDB(byte[] content, HistoryDatabaseFuncs.DBEntry entry)
        {
            var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Meta",entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part));
            var key = HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part);
            byte[] value = { };
            string type = "Zip";
            if (meta?[4] == 2)
            {
                type = "Text";
                value = content;
            }
            else
            {
                MemoryStream contentMemStream = new MemoryStream(content);
                MemoryStream outputMemStream = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

                zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

                ZipEntry newEntry = new ZipEntry(entry.Period + ".txt");
                newEntry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(newEntry);

                StreamUtils.Copy(contentMemStream, zipStream, new byte[4096]);
                zipStream.CloseEntry();

                zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
                zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

                outputMemStream.Position = 0;
                value = outputMemStream.ToArray();
            }
            var metaPair = GetRebuildedMeta(content, type, entry);
            KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>> res = new KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>(new KeyValuePair<byte[], byte[]>(key, value), new KeyValuePair<byte[], byte[]>(metaPair.Key, metaPair.Value));
            return res;  
        }


        public KeyValuePair<byte[],byte[]> GetRebuildedMeta(byte[] TextContent, string type, HistoryDatabaseFuncs.DBEntry entry)
        {
            Crc32 hash = new Crc32();
            hash.Update(TextContent);
            var metaKey = HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Meta", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part);
            byte[] GettedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash.Value)).CopyTo(GettedEntry, 0);
            if (type == "Zip")
                GettedEntry[4] = 1;
            if (type == "Text")
                GettedEntry[4] = 2;
            return new KeyValuePair<byte[], byte[]>(metaKey, GettedEntry);
        }


        public void RebuildMeta(ChunkFile file, bool showMessages = true)
        {
            var path = HistoryDatabaseFuncs.GetPath(file);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            string MetaCorruptionMessage = "";
            var TypeAndtext = ReadFromDB(file);
            var Type = TypeAndtext.Key;
            var content = TypeAndtext.Value;
            Crc32 hash = new Crc32();
            hash.Update(content);
            var metaKey = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", file.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], file.Part);
            var it = _dbase.CreateIterator();
            it.Seek(metaKey);

            byte[] GettedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash.Value)).CopyTo(GettedEntry, 0);
            if (Type == "Zip")
                GettedEntry[4] = 1;
            if (Type == "Text")
                GettedEntry[4] = 2;
            if (showMessages)
            {
                if (!it.IsValid() || (!HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), metaKey, true, 4, true, true, true)))
                {
                    string pathStr = "";
                    foreach (var path_part in path)
                    {
                        if (pathStr != "")
                            pathStr += "/";
                        pathStr += (path_part.Name);
                    }
                    pathStr += ("." + file.Part + "");

                    MetaCorruptionMessage = "Meta for file " + pathStr + "was not found.\nMeta was recalculated";
                }
                else
                {
                    var metaEntry = it.GetValue();
                    Crc32 hashFromDB = new Crc32();
                    hashFromDB.Value = (BitConverter.ToUInt32(metaEntry, 0));
                    var metaStr = hashFromDB.Value.ToString("X8", CultureInfo.InvariantCulture);
                    metaStr += '\t';
                    if (metaEntry[4] == 1)
                        metaStr += "Zip";
                    if (metaEntry[4] == 2)
                        metaStr += "Text";


                    var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                    contStr += ('\t' + Type);
                    if (metaStr != contStr)
                    {
                        string pathStr = "";
                        foreach (var path_part in path)
                        {
                            if (pathStr != "")
                                pathStr += "/";
                            pathStr += (path_part.Name);
                        }
                        pathStr += ("." + file.Part + "");
                        MetaCorruptionMessage = "Meta for file " + pathStr + " was recalculated";
                    }
                }
            }
            _dbase.Put(metaKey, GettedEntry);
            it.Dispose();
            if (MetaCorruptionMessage != "" && showMessages)
            {
                MessageBox.Show(MetaCorruptionMessage, "Meta rebuild",MessageBoxButton.OK,MessageBoxImage.Asterisk);
            } 
        }


        public KeyValuePair<QHBar[], QHBar[]> GetM1FromTicks(IEnumerable<QHTick> ticks)
        {
            if (ticks == null || ticks.Count() == 0)
                return new KeyValuePair<QHBar[], QHBar[]>(null, null);

            List<QHBar> resBid = new List<QHBar>();
            List<QHBar> resAsk = new List<QHBar>();

            var curBid = new QHBar();
            var curAsk = new QHBar();

            curBid.Time = curAsk.Time = new DateTime(ticks.First().Time.Year, ticks.First().Time.Month, ticks.First().Time.Day, ticks.First().Time.Hour, ticks.First().Time.Minute, 0);
            curBid.Open = curBid.High = curBid.Low = curBid.Close = ticks.First().Bid;
            curAsk.Open = curAsk.High = curAsk.Low = curAsk.Close = ticks.First().Ask;

            foreach(var tick in ticks)
            {
                var time = new DateTime(tick.Time.Year, tick.Time.Month, tick.Time.Day, tick.Time.Hour, tick.Time.Minute, 0);
                if (curBid.Time!= time)
                {
                    resBid.Add(curBid);
                    curBid = new QHBar();
                    curBid.Time = time;
                    curBid.Open = curBid.High = curBid.Low = curBid.Close = tick.Bid;
                    curBid.Volume = 0;
                }
                if (curAsk.Time != time)
                {
                    resAsk.Add(curAsk);
                    curAsk = new QHBar();
                    curAsk.Time = time;
                    curAsk.Open = curAsk.High = curAsk.Low = curAsk.Close = tick.Ask;
                    curAsk.Volume = 0;
                }

                curBid.Close = tick.Bid;
                curBid.Volume += tick.BidVolume;
                curBid.High = Math.Max(curBid.High, tick.Bid);
                curBid.Low = Math.Min(curBid.Low, tick.Bid);

                curAsk.Close = tick.Ask;
                curAsk.Volume += tick.AskVolume;
                curAsk.High = Math.Max(curAsk.High, tick.Ask);
                curAsk.Low = Math.Min(curAsk.Low, tick.Ask);
            }
            resBid.Add(curBid);
            resAsk.Add(curAsk);

            return new KeyValuePair<QHBar[], QHBar[]>(resBid.ToArray(), resAsk.ToArray());
        }
        public QHTick[] GetTicksFromLevel2(IEnumerable<QHTickLevel2> ticks2)
        {
            if (ticks2 == null || ticks2.Count() == 0)
                return null;

            List<QHTick> res = new List<QHTick>();

            foreach (var t2 in ticks2)
            {
                res.Add(new QHTick()
                {
                    Part = t2.Part,
                    Time = t2.Time,
                    Bid = t2.BestBid.Key,
                    BidVolume = t2.BestBid.Value,
                    Ask = t2.BestAsk.Key,
                    AskVolume = t2.BestAsk.Value
                });
            }

            return res.ToArray();
        }
        void DeleteChunksAndMetaFromFolders(IList<Folder> Folders)
        {
            var toDel = new List<Folder>();

            foreach (var file in Folders)
            {
                if (file as ChunkFile != null || file as MetaFile != null)
                {
                    toDel.Add(file);
                }
            }

            toDel.ForEach(t => { Folders.Remove(t); });
        }

        void SvCntToFld(string content, string period, int part, Folder parent)
        {
            var chunk = new ChunkFile(period+" file", period, 0, parent);
            var meta = new MetaFile(period + " meta", period, 0, parent);
            parent.Folders.Add(chunk);
            parent.Folders.Add(meta);
            //SaveToDB(content, chunk);
            RebuildMeta(chunk);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> EnumerateFilesInFolder(Folder fold, List<string> periods = null, List<string> types = null, bool onlyKeys = false)
        {
            Stopwatch w = new Stopwatch();
            if (periods == null)
                periods = new List<string>() { "ticks", "ticks level2", "M1 ask", "M1 bid" };
            if (types == null)
                types = new List<string>() { "Meta", "Chunk" };

            var it = _dbase.CreateIterator();

            if (fold as ChunkFile == null && fold as MetaFile == null)
            {
                var path = HistoryDatabaseFuncs.GetPath(fold);
                if(path.Count>4)
                    periods = new List<string>() { "ticks", "ticks level2"};
                int[] dateTime = { 2000, 1, 1, 0 };
                for (int i = 1; i < path.Count; i++)
                {
                    dateTime[i - 1] = int.Parse(path[i].Name);
                }

                foreach (var period in HistoryDatabaseFuncs.periodicityDict)
                {
                    if(periods.Contains(period.Key))
                    foreach (var type in HistoryDatabaseFuncs.typeDict)
                    {
                            if (types.Contains(type.Key))
                            {
                                var key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, type.Key, period.Key, dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0);
                                it.Seek(key);
                                List<KeyValuePair<byte[], byte[]>> resList = new List<KeyValuePair<byte[], byte[]>>();
                                while (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 1, true, true, false, false))
                                {
                                    if (resList.Count < 128)
                                        resList.Add(new KeyValuePair<byte[], byte[]>(it.GetKey(), onlyKeys?null:it.GetValue()));
                                    else
                                    {
                                        foreach (var pair in resList)
                                            yield return pair;
                                        resList = new List<KeyValuePair<byte[], byte[]>>();
                                    }
                                    it.Next();
                                }
                                foreach (var pair in resList)
                                    yield return pair;
                            }
                    }
                }
            }
            else
            {
                byte[] key = new byte[] { };
                var path = HistoryDatabaseFuncs.GetPath(fold);
                int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
                string type = "";
                if (fold as ChunkFile != null)
                {
                    ChunkFile chunk = fold as ChunkFile;
                    key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", chunk.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], chunk.Part);
                    type = "Chunk";
                    it.Seek(key);
                }
                if (fold as MetaFile != null)
                {
                    MetaFile metaFile = fold as MetaFile;
                    key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                    type = "Meta";
                    it.Seek(key);
                }
                if (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, path.Count - 2, true, true, true))
                {
                    var file = fold as HistoryFile;
                    if(periods.Contains(file.Period) && types.Contains(type))
                    yield return new KeyValuePair<byte[], byte[]>(it.GetKey(), onlyKeys ? null : it.GetValue());
                }
            }

            it.Dispose();
        }
    }
   
}
