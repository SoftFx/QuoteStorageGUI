using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static QuoteHistoryGUI.HistoryTools.HistoryDatabaseFuncs;

namespace QuoteHistoryGUI.HistoryTools.MetaStorage
{

    class MetaCollection
    {
        HistoryLoader _loader;
        public int Version = 0;
        public string Symbol;
        public string Period;
        private List<DBEntry> Meta;

        public MetaCollection(HistoryLoader loader, string symbol, string period) {
            _loader = loader;
            Symbol = symbol;
            Period = period;
        }

        public IEnumerable<DBEntry> GetMeta(int version)
        {
            if(version != Version || Meta==null)
            {
                Meta = _loader.ReadMeta(Symbol, Period).ToList();
                Version = version;
            }
            return Meta;
        }
    }

    public class MetaStorage
    {
        public int Version = 0;
        Dictionary<KeyValuePair<string, string>, MetaCollection> MetaDict;
        HistoryLoader _loader;
        public MetaStorage(HistoryLoader loader)
        {
            _loader = loader;
            MetaDict = new Dictionary<KeyValuePair<string, string>, MetaCollection>();
        }

        public IEnumerable<DBEntry> GetMeta(string symbol, string period)
        {
            MetaCollection collection;
            if(MetaDict.TryGetValue(new KeyValuePair<string, string>(symbol, period),out collection))
                return collection.GetMeta(Version);
            else
            {
                var newCollection = new MetaCollection(_loader, symbol, period);
                MetaDict.Add(new KeyValuePair<string, string>(symbol, period), newCollection);
                return newCollection.GetMeta(Version);
            }
        }


    }
}
