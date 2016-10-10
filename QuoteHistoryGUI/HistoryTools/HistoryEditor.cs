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

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryEditor
    {
        

        private DB _dbase;
        public HistoryEditor(DB db)
        {
            _dbase = db;
        }

        public string GetText(byte[] content)
        {
            bool isZip = false;
            if (content[0] == 'P' && content[1] == 'K')
                isZip = true;
            if (!isZip)
                return ASCIIEncoding.ASCII.GetString(content);
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
                return ASCIIEncoding.ASCII.GetString(content);
            }
        }
        public KeyValuePair<string,string> ReadFromDB(HistoryFile f) {

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);

            string period = f.Period;
            byte[] content = { };
            bool isZip = false;
            if (f as ChunkFile != null)
            {
                var cnt = _dbase.Get(HistoryLoader.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                if (cnt[0] == 'P' && cnt[1] == 'K')
                    isZip = true;
                var Text = GetText(cnt);
                return new KeyValuePair<string, string>(isZip ? "Zip" : "Text", Text);
            }
            else
            {
                var meta = _dbase.Get(HistoryLoader.SerealizeKey(path[0].Name, "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                // (BitConverter.ToUInt32(dbVal, 0));
                Crc32 hash = new Crc32();
                hash.Value = (BitConverter.ToUInt32(meta, 0));
                var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                contStr += '\t';
                if (meta[4] == 1)
                    contStr += "Zip";
                if (meta[4] == 2)
                    contStr += "Text";
                return new KeyValuePair<string, string>("Meta", contStr);

            }
            
        }

        public void SaveToDB(string content, ChunkFile f)
        {
            try
            {
                HistorySerializer.Deserialize(f.Period, ASCIIEncoding.ASCII.GetBytes(content));
            }
            catch
            {
                MessageBox.Show("There is a syntax error! Unable to save.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.MainWindow.Activate();
                return;
            }

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            string period = f.Period;
            var meta = _dbase.Get(HistoryLoader.SerealizeKey(path[0].Name, "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));

            var key = HistoryLoader.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part);
            byte[] value = { };
            if (meta[4] == 2)
            {
                value = ASCIIEncoding.ASCII.GetBytes(content.ToArray());   
            }
            if (meta[4] == 1)
            {
                MemoryStream contentMemStream = new MemoryStream(ASCIIEncoding.ASCII.GetBytes(content.ToArray()));
                MemoryStream outputMemStream = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

                zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

                ZipEntry newEntry = new ZipEntry("zip");
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
            RebuildMeta(f);
        }


        public void RebuildMeta(ChunkFile file)
        {
            var path = HistoryDatabaseFuncs.GetPath(file);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            string MetaCorruptionMessage = "";
            var TypeAndtext = ReadFromDB(file);
            var Type = TypeAndtext.Key;
            var text = TypeAndtext.Value;
            Crc32 hash = new Crc32();
            hash.Update(ASCIIEncoding.ASCII.GetBytes(text));
            var metaKey = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", file.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], file.Part);
            var it = _dbase.CreateIterator();
            it.Seek(metaKey);

            byte[] GettedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash.Value)).CopyTo(GettedEntry, 0);
            if (Type == "Zip")
                GettedEntry[4] = 1;
            if (Type == "Text")
                GettedEntry[4] = 2;

            if (!it.IsValid() || (!HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), metaKey, true, 4, true, true, true)))
            {
                string pathStr = "";
                foreach(var path_part in path)
                {
                    pathStr += (path_part.Name + "/");
                }
                pathStr += (file.Name + " (" + file.Part + ")");

                MetaCorruptionMessage = "Meta for file " + pathStr + "was not found.\n Meta was recalculated";
                _dbase.Put(metaKey, GettedEntry);
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
                contStr += ('\t'+Type);
                if(metaStr != contStr)
                {
                    string pathStr = "";
                    foreach (var path_part in path)
                    {
                        pathStr += (path_part.Name + "/");
                    }
                    pathStr += (file.Name + " (" + file.Part + ")");
                    MetaCorruptionMessage = "Meta for file " + pathStr + " was corrupted (invalid hash or file type).\n Meta was recalculated";
                    
                    
                }
            }
            _dbase.Put(metaKey, GettedEntry);
            it.Dispose();
            if (MetaCorruptionMessage != "")
            {
                MessageBox.Show(MetaCorruptionMessage, "Meta rebuild",MessageBoxButton.OK,MessageBoxImage.Asterisk);
            }
            Application.Current.MainWindow.Activate();
        }

    }
}
