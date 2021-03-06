﻿using QuoteHistoryGUI.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static QuoteHistoryGUI.HistoryTools.HistoryDatabaseFuncs;
using System.Text.RegularExpressions;

namespace QuoteHistoryGUI.HistoryTools.Interactor
{
    /*
   class SubStrNode
   {
       public string Str;
       public List<SubStrNode> ChildList;
       public SubStrNode Parent;
       public bool used;

       public SubStrNode()
       {
           ChildList = new List<SubStrNode>();
       }
       public SubStrNode(SubStrNode parent) : this()
       {
           Parent = parent;
       }

   }

  class SmartTemplate
   {
       SubStrNode startNode;

       public SmartTemplate(string strTemplate)
       {
           int curInd = 0;
           int bufInd = 0;

           startNode = new SubStrNode();
           var currentNode = new SubStrNode(startNode);
           startNode.ChildList.Add(currentNode);

           for (int i = 0; i < strTemplate.Length; i++)
           {
               if (strTemplate[i] == '(')
               {

               }
               else if (strTemplate[i] == ')')
               {

               }
               else if (strTemplate[i] == '|')
               {

               }
           }

       }
   }*/

    public class SelectTemplateWorker
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
            result.Add(template.Substring(0, ind));
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
                foreach (var alt in alternatives)
                {
                    foreach (var temp in result)
                    {
                        tempRes.Add(temp + alt);
                    }
                }
                result = new List<string>(tempRes);
                curTemplate = curTemplate.Substring(closeInd + 1);
                ind = curTemplate.IndexOf('(');
                if (ind == -1) { for (int i = 0; i < result.Count(); i++) { result[i] += curTemplate; } break; }
                for (int i = 0; i < result.Count(); i++) { result[i] += curTemplate.Substring(0, ind); }
                curTemplate = curTemplate.Substring(ind + 1);
            }
            return result.ToArray();
        }


        public IEnumerable<Folder> GetByMatch(string itemplate, BackgroundWorker worker = null, bool fillToTicksPath = false)
        {
            if(worker!=null)
                worker.ReportProgress(1, "Template processing: " + itemplate);

            if (itemplate == "")
                yield break;
            var res = new List<Folder>();
            DateTime lastReport = DateTime.UtcNow.AddSeconds(-2);
            int matchedCnt = 0;
            StringBuilder builder = new StringBuilder();

            foreach (var sym in itemplate)
            {
                if(sym=='*')
                    builder.Append('.');
                builder.Append(sym);
            }
            var template = builder.ToString();
            

            var result = new List<Folder>();
            var wordTemplates = new List<string>(template.Split(new char[] { '/', '\\', ';', '\t' }));
            var templateExp = new List<Regex>();
            if (fillToTicksPath)
            {
                while (wordTemplates.Count != 6)
                    wordTemplates.Add(".*");
            }

            foreach (var word in wordTemplates)
                templateExp.Add(new Regex("^"+word+"$"));


            var matchedFolders = new List<Folder>(_sourceTree);
            var matchedFiles = new List<HistoryFile>();
            var n_matchedFolders = new List<Folder>();
            Stack<KeyValuePair<Folder, int>> matchingStack = new Stack<KeyValuePair<Folder, int>>();
            foreach (var fold in _sourceTree)
                matchingStack.Push(new KeyValuePair<Folder, int>(fold, 0));
            int templateSize = wordTemplates.Count();
            while (matchingStack.Count != 0)
            {
                if (worker != null && worker.CancellationPending)
                {
                    yield break;
                }

                if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                {
                    worker.ReportProgress(1, "Matched files and folders count: " + matchedCnt);
                    lastReport = DateTime.UtcNow;
                }

                var currentPair = matchingStack.Pop();
                var fold = currentPair.Key;
                var level = currentPair.Value;
                var match = templateExp[level].Match(fold.Name);
                if (match.Success) //Match(fold.Name, wordTemplates[level]))
                {
                    if (level == templateSize - 1)
                    {
                        matchedCnt++;
                        yield return currentPair.Key;
                    }
                    else
                    {
                        if (!fold.Loaded)
                        {
                            if (fold as ChunkFile == null && fold as MetaFile == null)
                                _loader.ReadDateTimes(fold);
                            fold.Loaded = true;
                        }
                        if (fold.Folders != null)
                            foreach (var child_folder in fold.Folders)
                            {
                                matchingStack.Push(new KeyValuePair<Folder, int>(child_folder, level + 1));
                            }
                    }
                }
            }
        }

        List<string> getPathFromMetaEntry(DBEntry entry)
        {
            List<string> result = new List<string>();
            result.Add(entry.Symbol);
            result.Add(entry.Time.Year.ToString());
            result.Add(entry.Time.Month.ToString());
            result.Add(entry.Time.Day.ToString());
            if (entry.Period != "M1 bid" && entry.Period != "M1 ask")
            {
                result.Add(entry.Time.Hour.ToString());
            }
            result.Add(entry.Period);
            return result;
        }

        public IEnumerable<DBEntry> GetFromMetaByMatch(List<string> itemplates, StorageInstanceModel source, BackgroundWorker worker = null, bool fillToTicksPath = false)
        {
            DateTime lastReport = DateTime.UtcNow.AddSeconds(-2);
            var templates = new List<string>();
            foreach (var orTemplate in itemplates)
            {
                templates.AddRange(GetORTemplates(orTemplate));
            }
            var wordTemplates = new List<List<string>>();
            foreach (var template in templates)
            {
                wordTemplates.Add(new List<string>(template.Split(new char[] { '/', '\\', ';', '\t' })));
            }
            List<string> symbols = new List<string>();
            foreach (var fold in source.Folders)
            {
                foreach (var wordTempl in wordTemplates)
                {
                    if (wordTempl[0].Trim() == "*" || wordTempl[0].Trim() == fold.Name)
                    {
                        symbols.Add(fold.Name);
                        break;
                    }
                }
            }
            bool add = true;



            foreach (var symbol in symbols)
            {

                foreach (var period in HistoryDatabaseFuncs.periodicityDict.Keys)
                {
                    int cnt = 0;
                    int allCnt = source.MetaStorage.GetMeta(symbol, period).Count();
                    var metas = source.MetaStorage.GetMeta(symbol, period).ToArray();
                    foreach (var meta in metas)
                    {
                        cnt++;
                        if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                        {
                            worker.ReportProgress(1, "Matching and Copying files : " + symbol + " " + cnt + "/" + allCnt);
                            lastReport = DateTime.UtcNow;
                        }
                        foreach (var template in wordTemplates)
                        {
                            var pathWords = getPathFromMetaEntry(meta);
                            add = true;
                            for (int i = 0; i < template.Count(); i++)
                            {
                                if (!Match(pathWords[i], template[i]))
                                {
                                    add = false;
                                    break;
                                }
                            }
                            if (add) yield return meta;
                        }
                    }
                }
            }
        }



        public bool Match(string word, string template)
        {
            template.Trim(' ');
            if (template == "") return false;
            bool anyEnd = template.Last() == '*';
            bool anyStart = template.First() == '*';

            var templParts = new List<string>();
            (new List<string>(template.Split('*'))).ForEach(t => { if (t != "") templParts.Add(t); });

            if (templParts.Count == 0)
                return anyStart || anyEnd;

            if (!anyStart)
            {
                if ((word.Length < templParts[0].Length) || word.Substring(0, templParts[0].Length) != templParts[0]) return false;
                word = word.Substring(templParts[0].Length);
                templParts.RemoveAt(0);
            }

            foreach (var part in templParts)
            {
                var ind = word.IndexOf(part);
                if (ind == -1) return false;
                word = word.Substring(ind + part.Length);
            }

            return word.Length == 0 || anyEnd;
        }
    }
}
