using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI.HistoryTools.Interactor
{
    class SelectTemplateWorker
    {
        IEnumerable<Folder> _sourceTree;
        HistoryLoader _loader;
        public SelectTemplateWorker(IEnumerable<Folder> tree, HistoryLoader loader)
        {
            _loader = loader;
            _sourceTree = tree;
        }

        public IEnumerable<Folder> GetByMatch(string template, BackgroundWorker worker = null)
        {
            if (worker != null)
            {
                worker.ReportProgress(1, "Matching "+template);
            }
            var result = new List<Folder>();
            var wordTemplates = template.Split(new char[]{ '/','\\',';','\t'});
            var matchedFolders = new List<Folder>(_sourceTree);
            var matchedFiles = new List<HistoryFile>();
            var n_matchedFolders = new List<Folder>();
            foreach (var wordTemplate in wordTemplates)
            {
                
                n_matchedFolders = new List<Folder>();
                foreach (var folder in matchedFolders)
                {
                    if (Match(folder.Name, wordTemplate))
                    {
                        n_matchedFolders.Add(folder);
                    }
                }
                matchedFolders = new List<Folder>();
                foreach (var folder in n_matchedFolders)
                {
                    if (!folder.Loaded)
                    {   
                        if(folder as ChunkFile == null && folder as MetaFile == null)
                            _loader.ReadDateTimes(folder);
                        folder.Loaded = true;
                    }
                    if(folder.Folders!=null)
                    foreach (var child_folder in folder.Folders)
                    {
                        matchedFolders.Add(child_folder);
                    }
                }
            }
            return n_matchedFolders;
        }

        public bool Match(string word, string template)
        {
            template.Trim(' ');
            if (template == "") return false;
            bool anyEnd = template.Last() == '*';
            bool anyStart = template.First() == '*';

            var templParts = new List<string>();
            (new List<string>(template.Split('*'))).ForEach(t => { if (t != "") templParts.Add(t);});

            if (templParts.Count == 0)
                return anyStart || anyEnd;

            if (!anyStart)
            {
                if ((word.Length < templParts[0].Length) || word.Substring(0, templParts[0].Length) != templParts[0]) return false;
                word = word.Substring(templParts[0].Length);
                templParts.RemoveAt(0);
            }

            foreach(var part in templParts)
            {
                var ind = word.IndexOf(part);
                if (ind == -1) return false;
                word = word.Substring(ind + part.Length);
            }

            return word.Length == 0 || anyEnd;
        }
    }
}
