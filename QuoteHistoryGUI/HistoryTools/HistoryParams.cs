using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI.HistoryTools
{
    class HistoryParams
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
    }
}
