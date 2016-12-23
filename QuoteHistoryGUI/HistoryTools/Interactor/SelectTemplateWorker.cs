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

        public string[] GetORTemplates(string template)
        {
            List<string> result = new List<string>();
            var ind = template.IndexOf('(');
            if (ind == -1)
                return new string[] { template };
            result.Add(template.Substring(0,ind));
            var curTemplate = template.Substring(ind + 1);
            while (true)
            {
                var closeInd = curTemplate.IndexOf(')');
                if (closeInd == -1)
                {
                    if (curTemplate != "")
                        throw (new InvalidOperationException("Unable to parse template. \")\" symbol not found."));
                    break;
                }
                var alternatives = curTemplate.Substring(0, closeInd).Split('|');
                List<string> tempRes = new List<string>();
                foreach(var alt in alternatives)
                {
                    foreach(var temp in result)
                    {
                        tempRes.Add(temp + alt);
                    }
                }
                result = new List<string>(tempRes);
                curTemplate = curTemplate.Substring(closeInd + 1);
                ind = curTemplate.IndexOf('(');
                if (ind == -1) { for (int i = 0; i < result.Count(); i++) { result[i] += curTemplate; } break; }
                for (int i = 0; i < result.Count(); i++) { result[i] += curTemplate.Substring(0,ind); }
                curTemplate = curTemplate.Substring(ind + 1);
            }
            return result.ToArray();
        }


        public IEnumerable<Folder> GetByMatch(string itemplate, BackgroundWorker worker = null, bool fillToTicksPath = false)
        {
            var templates = GetORTemplates(itemplate);
            var res = new List<Folder>();
            DateTime lastReport = DateTime.UtcNow;
            string mes = "";
            foreach (var template in templates)
            {
                
                var result = new List<Folder>();
                var wordTemplates = new List<string>(template.Split(new char[] { '/', '\\', ';', '\t' }));
                if (fillToTicksPath)
                {
                    while (wordTemplates.Count != 6)
                        wordTemplates.Add("*");
                }

                var matchedFolders = new List<Folder>(_sourceTree);
                var matchedFiles = new List<HistoryFile>();
                var n_matchedFolders = new List<Folder>();
                foreach (var wordTemplate in wordTemplates)
                {
                    n_matchedFolders = new List<Folder>();
                    foreach (var folder in matchedFolders)
                    {
                        if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                        {
                            worker.ReportProgress(1, "Matching template : " + template + mes);
                            if (mes.Length < 3) mes += "."; else mes = "";
                            lastReport = DateTime.UtcNow;
                        }
                        if (Match(folder.Name, wordTemplate))
                        {
                            n_matchedFolders.Add(folder);
                        }
                    }
                    matchedFolders = new List<Folder>();
                    foreach (var folder in n_matchedFolders)
                    {
                        if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                        {
                            worker.ReportProgress(1, "Matching template : " + template + mes);
                            if (mes.Length < 3) mes += "."; else mes = "";
                            lastReport = DateTime.UtcNow;
                        }
                        if (!folder.Loaded)
                        {
                            if (folder as ChunkFile == null && folder as MetaFile == null)
                                _loader.ReadDateTimes(folder);
                            folder.Loaded = true;
                        }
                        if (folder.Folders != null)
                            foreach (var child_folder in folder.Folders)
                            {
                                matchedFolders.Add(child_folder);
                            }
                    }
                }
                res.AddRange(n_matchedFolders);
            }
            return res;
        }

        public IEnumerable<Folder> GetFromMetaByMatch(string itemplate, BackgroundWorker worker = null, bool fillToTicksPath = false)
        {
            var templates = GetORTemplates(itemplate);
            var res = new List<Folder>();
            DateTime lastReport = DateTime.UtcNow;
            string mes = "";
            foreach (var template in templates)
            {

                var result = new List<Folder>();
                var wordTemplates = new List<string>(template.Split(new char[] { '/', '\\', ';', '\t' }));
                if (fillToTicksPath)
                {
                    while (wordTemplates.Count != 6)
                        wordTemplates.Add("*");
                }

                var matchedFolders = new List<Folder>(_sourceTree);
                var matchedFiles = new List<HistoryFile>();
                var n_matchedFolders = new List<Folder>();
                foreach (var wordTemplate in wordTemplates)
                {
                    n_matchedFolders = new List<Folder>();
                    foreach (var folder in matchedFolders)
                    {
                        if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                        {
                            worker.ReportProgress(1, "Matching template : " + template + mes);
                            if (mes.Length < 3) mes += "."; else mes = "";
                            lastReport = DateTime.UtcNow;
                        }
                        if (Match(folder.Name, wordTemplate))
                        {
                            n_matchedFolders.Add(folder);
                        }
                    }
                    matchedFolders = new List<Folder>();
                    foreach (var folder in n_matchedFolders)
                    {
                        if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                        {
                            worker.ReportProgress(1, "Matching template : " + template + mes);
                            if (mes.Length < 3) mes += "."; else mes = "";
                            lastReport = DateTime.UtcNow;
                        }
                        if (!folder.Loaded)
                        {
                            if (folder as ChunkFile == null && folder as MetaFile == null)
                                _loader.ReadDateTimes(folder);
                            folder.Loaded = true;
                        }
                        if (folder.Folders != null)
                            foreach (var child_folder in folder.Folders)
                            {
                                matchedFolders.Add(child_folder);
                            }
                    }
                }
                res.AddRange(n_matchedFolders);
            }
            return res;
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
