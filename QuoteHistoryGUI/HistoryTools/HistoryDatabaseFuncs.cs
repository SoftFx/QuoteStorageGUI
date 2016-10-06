using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI.HistoryTools
{
    static class HistoryDatabaseFuncs
    {
        static public readonly Dictionary<string, byte> periodicityDict = new Dictionary<string, byte>()
        {
            {"ticks",0 },
            {"ticks level2",1 },
            {"M1 ask",2 },
            {"M1 bid",3 },
        };

        static public readonly Dictionary<string, byte> typeDict = new Dictionary<string, byte>()
        {
            {"Meta",0 },
            {"Chunk",1 },
        };

        public static byte[] SerealizeKey(string sym, string type, string period, int year, int month, int day, int hour, int partNum = 0)
        {
            var _prefix = new byte[sym.Length + 2];
            ASCIIEncoding.ASCII.GetBytes(sym).CopyTo(_prefix, 0);
            _prefix[sym.Length] = (byte)(type == "Chunk" ? 1 : 0);
            _prefix[sym.Length + 1] = periodicityDict[period];
            var prefOffset = _prefix.Length;
            byte[] resKey = new byte[prefOffset + 5];
            _prefix.CopyTo(resKey, 0);
            UInt32 date = (uint)year;
            date = date * 100 + (uint)month;
            date = date * 100 + (uint)day;
            date = date * 100 + (uint)hour;
            var bd = BitConverter.GetBytes(date);
            resKey[prefOffset] = bd[3];
            resKey[prefOffset + 1] = bd[2];
            resKey[prefOffset + 2] = bd[1];
            resKey[prefOffset + 3] = bd[0];
            resKey[prefOffset + 4] = (byte)partNum;
            return resKey;
        }

        public static KeyValuePair<DateTime, int> GetDateAndPart(byte[] dbKey)
        {
            int i = 0;
            while (dbKey[i] > 1)
                i++;
            i += 2;
            byte part = (byte)(dbKey[i + 4]);
            byte[] dateByte = new byte[4];
            dateByte[0] = dbKey[i + 3];
            dateByte[1] = dbKey[i + 2];
            dateByte[2] = dbKey[i + 1];
            dateByte[3] = dbKey[i];
            UInt32 date = BitConverter.ToUInt32(dateByte, 0);
            int hour = (int)(date % 100);
            date = date / 100;
            int day = (int)(date % 100);
            date = date / 100;
            int month = (int)(date % 100);
            date = date / 100;
            int year = (int)date;

            return new KeyValuePair<DateTime, int>(new DateTime(year, month, day, hour, 0, 0), part);
        }


        public static bool ValidateKeyByKey(byte[] key1, byte[] key2, bool validateSymbol = true, int validationDateLevel = 1, bool validateType = false, bool validatePeriod = false, bool validatePart = false)
        {
            List<byte> sym1Bytes = new List<byte>();
            for (int i = 0; i < key1.Length; i++)
            {
                if (key1[i] > 1)
                    sym1Bytes.Add(key1[i]);
                else break;
            }
            List<byte> sym2Bytes = new List<byte>();
            for (int i = 0; i < key2.Length; i++)
            {
                if (key2[i] > 1)
                    sym2Bytes.Add(key2[i]);
                else break;
            }
            string sym1 = ASCIIEncoding.ASCII.GetString(sym1Bytes.ToArray());
            string sym2 = ASCIIEncoding.ASCII.GetString(sym2Bytes.ToArray());
            if (sym1 != sym2 && validateSymbol)
                return false;
            var dp1 = GetDateAndPart(key1);
            var dp2 = GetDateAndPart(key2);

            if (dp1.Key.Year != dp2.Key.Year && validationDateLevel > 0)
                return false;
            if (dp1.Key.Month != dp2.Key.Month && validationDateLevel > 1)
                return false;
            if (dp1.Key.Day != dp2.Key.Day && validationDateLevel > 2)
                return false;
            if (dp1.Key.Hour != dp2.Key.Hour && validationDateLevel > 3)
                return false;
            if (dp1.Value != dp2.Value && validatePart)
                return false;
            if (key1[sym1.Length] != key2[sym2.Length] && validateType)
                return false;
            if (key1[sym1.Length + 1] != key2[sym2.Length + 1] && validatePeriod)
                return false;
            return true;
        }

        public static List<Folder> GetPath(Folder fold)
        {
            var path = new List<Folder>();
            path.Add(fold);
            while (path.Last().Parent != null)
            {
                path.Add(path.Last().Parent);
            }
            path.Reverse();
            return path;
        }

        public static int[] GetFolderStartTime(List<Folder> path)
        {
            int[] dateTime = { 2000, 1, 1, 0 };
            for (int i = 1; i < path.Count - 1; i++)
            {
                dateTime[i - 1] = int.Parse(path[i].Name);
            }
            return dateTime;
        }

    }
}
