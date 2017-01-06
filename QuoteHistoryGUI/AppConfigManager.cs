using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI
{
    class AppConfigManager
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(AppConfigManager));
        public static void SavePathes(string path)
        {
            try
            {
                log.Info("Saving storage pathes...");
                var pathList = GetPathes();
                if (!pathList.Contains(path))
                {
                    pathList.Add(path);
                }
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                for (int i = 0; i < pathList.Count(); i++)
                {
                    if (config.AppSettings.Settings["path_" + i] == null)
                        config.AppSettings.Settings.Add(new KeyValueConfigurationElement("path_" + i, pathList[i]));
                    else
                        config.AppSettings.Settings["path_" + i].Value = pathList[i];

                }
                config.AppSettings.Settings["path_count"].Value = pathList.Count().ToString();
                config.Save();
                log.Info("Storage pathes saved");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        public static List<string> GetPathes()
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["path_count"] == null)
                    config.AppSettings.Settings.Add(new KeyValueConfigurationElement("path_count", "0"));
                var pathCount = int.Parse(config.AppSettings.Settings["path_count"].Value);
                var pathList = new List<string>();
                for (int i = 0; i < pathCount; i++)
                {
                    pathList.Add(config.AppSettings.Settings["path_" + i].Value);
                }
                config.Save();
                return pathList;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }
    }
}
