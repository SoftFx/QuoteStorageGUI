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

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryEditor
    {

        private DB _dbase;
        public HistoryEditor(DB db)
        {
            _dbase = db;
        }
        public string ReadFromDB(HistoryFile f) {
            List<string> path = new List<string>();
            Folder curFolder = f;
            while (true)
            {
                path.Add(curFolder.Name);
                curFolder = curFolder.Parent;
                if (curFolder == null)
                    break;
            }
            path.Reverse();
            int[] dateTime = { 2000, 1, 1, 0 };
            for (int i = 1; i < path.Count-1; i++)
            {
                dateTime[i - 1] = int.Parse(path[i]);
            }
            string period = f.Period;
            byte[] content = { };
            if (f as ChunkFile != null)
            {
                var meta = _dbase.Get(HistoryLoader.SerealizeKey(path[0], "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                bool isZip = false;
                
                if (meta == null)
                {
                    var cnt = _dbase.Get(HistoryLoader.SerealizeKey(path[0], "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                    if (cnt[0] == 'P' && cnt[1] == 'K')
                        isZip = true;
                }
                if (meta != null && meta[4] == 2)
                    content = _dbase.Get(HistoryLoader.SerealizeKey(path[0], "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                if ((meta != null && meta[4] == 1) || isZip == true)
                {
                    var zipContent = _dbase.Get(HistoryLoader.SerealizeKey(path[0], "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                    MemoryStream data = new MemoryStream(zipContent);
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
                }
            }
            else
            {
                var meta = _dbase.Get(HistoryLoader.SerealizeKey(path[0], "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                // (BitConverter.ToUInt32(dbVal, 0));
                Crc32 hash = new Crc32();
                hash.Value = (BitConverter.ToUInt32(meta, 0));
                var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                contStr += '\t';
                if (meta[4] == 1)
                    contStr += "Zip";
                if (meta[4] == 2)
                    contStr += "Text";
                content = ASCIIEncoding.ASCII.GetBytes(contStr);

            }
            return ASCIIEncoding.ASCII.GetString(content);
        }

        public void SaveToDB(string content, HistoryFile f)
        {
            List<string> path = new List<string>();
            Folder curFolder = f;
            while (true)
            {
                path.Add(curFolder.Name);
                curFolder = curFolder.Parent;
                if (curFolder == null)
                    break;
            }
            path.Reverse();
            int[] dateTime = { 2000, 1, 1, 0 };
            for (int i = 1; i < path.Count - 1; i++)
            {
                dateTime[i - 1] = int.Parse(path[i]);
            }
            string period = f.Period;
            var meta = _dbase.Get(HistoryLoader.SerealizeKey(path[0], "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));

            var key = HistoryLoader.SerealizeKey(path[0], "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part);
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
        }
    }
}
