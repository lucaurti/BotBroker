using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Broker.Common.Utility
{
    public class ConfigurationManager : IConfigurationManager
    {

        // variables
        private readonly IConfiguration myConfig;


        // init
        public ConfigurationManager(IConfiguration config) 
        {
            myConfig = config;
        }


        // functions
        public string GetParameterValue(string parameterName, string sectionName = "configurations")
        {
            var x = myConfig.GetSection(sectionName).AsEnumerable().ToList();
            return x.Where(s => s.Key.EndsWith(parameterName)).LastOrDefault().Value;
        }
        public List<string> GetParameterValueList(string parameterName, 
            string sectionName = "configurations", string subSectionName = null, int? Index = null)
        {
            var x = myConfig.GetSection(sectionName).AsEnumerable().ToList();
            if (subSectionName != null)
                x = x.Where(s => s.Key.Contains(subSectionName)).ToList();
            List<string> result = new List<string>();
            if (!Index.HasValue)
                foreach (var record in x.Where(s => s.Key.Contains(parameterName)).Select(s => s.Key).ToList())
                {
                    string[] split = record.Split(':');
                    if (split.Length != 5) continue;
                    if (!result.Contains(split[3]))
                        result.Add(split[3]);
                }
            else
            {
                foreach (var record in x.Where(s => s.Key.Contains(parameterName)).ToList())
                {
                    string[] split = record.Key.Split(':');
                    if (split.Length != 5) continue;
                    if (Index.Value == int.Parse(split[3]))
                        result.Add(record.Value);
                }
            }
            return result.OrderBy(s => s).ToList();
        }
        public bool ExistsParameter(string parameterName, string sectionName = "configurations", bool contains = false)
        {
            var x = myConfig.GetSection(sectionName).AsEnumerable().ToList();
            var y = x.Where(s => s.Key.ToLower().EndsWith(parameterName.ToLower())).Count();
            if (y > 0) return true;
            y = x.Where(s => s.Key.ToLower().Contains(parameterName.ToLower())).Count();
            if (y > 0) return true;
            return false;
        }
        public string FindParameter(string parameterName, string sectionName = "configurations")
        {
            var x = myConfig.GetSection(sectionName).AsEnumerable().ToList();
            var y = x.Where(s => s.Key.ToLower().Contains(parameterName.ToLower())).Count();
            if (y > 0) 
            {
                string key = x.Where(s => s.Key.ToLower().Contains(parameterName.ToLower())).Last().Key;
                if (!key.Contains(':')) return key;
                else return key.Substring(key.LastIndexOf(':') + 1);
            }
            return null;
        }


        // static properties
        public static IConfiguration GetConfiguration
        {
            get
            {
                return new ConfigurationBuilder()
                    .AddJsonFile(GetPathConfigFile, true, true)
                    .Build();
            }
        }
        public static string GetPathConfigFile
        {
            get { return Path.Combine(AppContext.BaseDirectory, "Exposed", "appsettings.json"); }
        }
        public static string GetPathDatabaseFile
        {
            get { return Path.Combine(AppContext.BaseDirectory, "Exposed", "broker.db"); }
        }


        // properties
        public bool isDebugLog
        {
            get
            {
                return bool.Parse(GetParameterValue("isDebugLog"));
            }
        }
        public bool mustStartBatch
        {
            get
            {
                return bool.Parse(GetParameterValue("mustStartBatch"));
            }
        }
        public bool mustStartWeb
        {
            get
            {
                return bool.Parse(GetParameterValue("mustStartWeb"));
            }
        }
        public bool mustStartTelegram
        {
            get
            {
                return bool.Parse(GetParameterValue("mustStartTelegram"));
            }
        }

    }
}