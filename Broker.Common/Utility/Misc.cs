using System.Linq;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Globalization;
using Broker.Common.WebAPI.Models;
using System.Collections.Generic;
using Broker.Common.WebAPI;

namespace Broker.Common.Utility
{
    public static class Misc
    {
        
        // enum
        public enum CacheType
        {
            Load,
            Save,
            Delete

        }


        // properties
        public static string GetExchange 
        {
            get
            {
                string exchange = GetParameterValue("apiExchange", "services");
                return exchange.Replace("WebAPI", "").Replace(".", "");
            }
        }
        public static string GetStrategy 
        {
            get
            {
                string exchange = GetParameterValue("apiStrategy", "services");
                return exchange.Replace("Strategies", "").Replace("Strategy", "").Replace(".", "");
            }
        }
        public static int GetTickerTime
        {
            get
            {
                return int.Parse(GetParameterValue("secondsTicker"));
            }
        }
        public static int GetCandleTime
        {
            get
            {
                return int.Parse(GetParameterValue("minutesCandle"));
            }
        }
        public static bool IsPaperTrade
        {
            get
            {
                return bool.Parse(GetParameterValue("isPaperTrade"));
            }
        }
        public static bool MustRecoverData
        {
            get
            {
                return bool.Parse(GetParameterValue("mustRecoverData"));
            }
        }
        public static string GetTelegramToken
        {
            get
            {
                var x = Misc.GetParameterValue("token", "telegram");
                return (x == "") ? null : x;
            }
        }
        public static string GetTelegramPassword
        {
            get
            {
                var x = Misc.GetParameterValue("password", "telegram");
                return (x == "") ? null : x;
            }
        }     
        public static string GetTelegramUsernameTo
        {
            get
            {
                var x = Misc.GetParameterValue("usernameTo", "telegram");
                return (x == "") ? null : x;
            }
        }  
        public static TimeSpan RoundDateTimeCandle
        {
            get 
            {
                DateTime now = DateTime.Now;
                DateTime x = now.RoundUpDateCandle();
                long ticks = x.Ticks - DateTime.Now.Ticks;
                if (ticks < 0) 
                    x = now.AddMinutes(1).RoundUpDateCandle();
                ticks = x.Ticks - DateTime.Now.Ticks;
                return new TimeSpan(ticks);
            }
        }

        public static bool GetLogStrategyOnCandleUpdate
        {
            get
            {
                return bool.Parse(Misc.GetParameterValue("logStrategyOnCandleUpdate"));
            }
        }
        

        // extension
        public static DateTime ToDateTime(this uint timestamp) => ((UInt64)timestamp).ToDateTime();
        public static DateTime ToDateTime(this UInt64 timestamp) => new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(timestamp);
        public static DateTime ToDateTime(this long timestamp) => new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(timestamp);
        public static string ToShortDateTimeString(this DateTime date) => date.ToShortDateStringEU() + " " + date.ToShortTimeStringEU();
        public static decimal Round(this decimal price, int precision) => Math.Round(price, precision);
        public static uint ToEpochTime(this DateTime dateTime) => (uint)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
        public static string ToUnixTimeStamp(this DateTime baseDateTime)
        {
            var dtOffset = new DateTimeOffset(baseDateTime);
            return dtOffset.ToUnixTimeMilliseconds().ToString();
        }
        public static string ToShortDateStringEU(this DateTime dateTime) => dateTime.ToString("dd/MM/yyyy");
        public static string ToShortTimeStringEU(this DateTime dateTime) => dateTime.ToString("HH:mm:ss");
        public static string ToStringRound(this decimal price, int precision) 
        {            
            return (precision == 0) ? 
                Math.Truncate(Math.Round(price, precision)).ToString("F0") :
                Math.Round(price, precision).ToString("F" + precision);
        }
        public static T DeepClone<T>(this T obj)
        {
            if (obj == null) return default(T);
            MemoryStream ms = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, obj);
            ms.Position = 0;
            return (T)formatter.Deserialize(ms);
        }
        public static bool isNumeric(this string input) 
        {
            float output;
            return float.TryParse(input, out output);
        }
        public static decimal ToDecimal(this string input)
        {

            if (input == null) return 0;
            if (input == "") return 0;
            if (input == "0") return 0;

            var commaCulture = new CultureInfo("en") { NumberFormat = { NumberDecimalSeparator = "," } };
            var pointCulture = new CultureInfo("en") { NumberFormat = { NumberDecimalSeparator = "." } };
            input = input.Trim();

            if (input.isNumeric() && !input.Contains(".") && !input.Contains(",")) 
                return Convert.ToDecimal(input);

            if (input.Contains(",") && input.Split(',').Length == 2)
                return Convert.ToDecimal(input, commaCulture);

            if (input.Contains(".") && input.Split('.').Length == 2)
                return Convert.ToDecimal(input, pointCulture);

            throw new Exception("Invalid input!");
        }

        public static dynamic CacheManager(this string key, CacheType operation, dynamic value = null) 
        {
            MySetup setup;
            BrokerDBContext db = new BrokerDBContext();

            switch (operation) 
            {
                case CacheType.Delete:
                    setup = db.MySetups.Where(s=>s.Key == key).First();
                    db.MySetups.Remove(setup);
                    return (db.SaveChanges() > 0);

                case CacheType.Load:
                    setup = db.MySetups.Where(s=>s.Key == key).FirstOrDefault();
                    return (setup != null ? setup.Value: null);

                case CacheType.Save:
                    if (value != null) {
                        setup = db.MySetups.Where(s=>s.Key == key).FirstOrDefault();
                        if (setup == null) {
                            setup = new MySetup();
                            setup.Key = key;
                            setup.Value = value.ToString();
                            db.MySetups.Add(setup);
                        }
                        else
                            setup.Value = value.ToString();
                        return (db.SaveChanges() > 0);
                    }
                    break;

            }

            return false;
        }
        public static DateTime RoundUpDateCandle(this DateTime dateTime)
        {
            int candleTime = GetCandleTime;
            return new DateTime(dateTime.Year, dateTime.Month, 
                dateTime.Day, dateTime.Hour, dateTime.Minute, 0)
                .AddMinutes(dateTime.Minute % candleTime == 0 ? 
                0 : candleTime - dateTime.Minute % candleTime);
        }
        
        
        // multicoin
        public static MyWebAPISettings GenerateMyWebAPISettings(int? numInstance = null) 
        {
            MyWebAPISettings settings;
            if (!numInstance.HasValue) 
                settings= new MyWebAPISettings
                (
                    Asset: Misc.GetParameterValue("asset", Misc.GetExchange), 
                    Currency: Misc.GetParameterValue("currency", Misc.GetExchange),
                    Separator: Misc.GetParameterValue("separator", Misc.GetExchange),
                    PrecisionAsset: int.Parse(Misc.GetParameterValue("precisionAsset", Misc.GetExchange)),
                    PrecisionCurrency: int.Parse(Misc.GetParameterValue("precisionCurrency", Misc.GetExchange))
                );
            else
                settings= new MyWebAPISettings
                (
                    Asset: Misc.GetParameterValueList("asset", numInstance, Misc.GetExchange, "multicoin"), 
                    Currency: Misc.GetParameterValueList("currency", numInstance, Misc.GetExchange, "multicoin"),
                    Separator: Misc.GetParameterValue("separator", Misc.GetExchange),
                    PrecisionAsset: int.Parse(Misc.GetParameterValueList("precisionAsset", numInstance, Misc.GetExchange, "multicoin")),
                    PrecisionCurrency: int.Parse(Misc.GetParameterValueList("precisionCurrency", numInstance, Misc.GetExchange, "multicoin"))
                );
            return settings.GenerateMyWebAPISettings();
        }
        public static MyWebAPISettings GenerateMyWebAPISettings(this MyWebAPISettings Settings, BrokerDBContext db = null) 
        {
            if (db == null) db = new BrokerDBContext();
            MyWebAPISettings settings = 
                db.MyWebAPISettings
                .Where(s => s.Asset == Settings.Asset && 
                    s.Currency == Settings.Currency)
                .FirstOrDefault();
            if (settings == null)
            {
                BrokerDBContext dbNew = new BrokerDBContext();
                Settings.Id = 0;
                dbNew.MyWebAPISettings.Add(Settings);
                dbNew.SaveChanges();
                return Settings.GenerateMyWebAPISettings(db);
            }
            return settings;
        }
        public static MyWebAPI ResolveWebAPI(this IList<MyWebAPI> WebAPIList, MyWebAPISettings Settings)
        {
            return WebAPIList.First(s => s.Settings.Id == Settings.Id);
        }


        // functions
        public static string GetParameterValue(string parameterName, string sectionName = "configurations")
        {
            //IConfigurationManager myService = ServiceLocator.Current.GetInstance<IConfigurationManager>();
            IConfigurationManager myService = new ConfigurationManager(ConfigurationManager.GetConfiguration);
            return myService.GetParameterValue(parameterName, sectionName);
        }
        public static string GetParameterValueList(string parameterName, int? index, string sectionName = "configurations", string subSectionName = null)
        {
            //IConfigurationManager myService = ServiceLocator.Current.GetInstance<IConfigurationManager>();
            IConfigurationManager myService = new ConfigurationManager(ConfigurationManager.GetConfiguration);
            return myService.GetParameterValueList(parameterName, sectionName, subSectionName, index).Last();
        }
        public static Type ToType(this string typeName, string prefix = "")
        {
            if (prefix != "") typeName = prefix + "." + typeName;
            var type = Type.GetType(typeName);
            if (type != null) return type;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName);
                if (type != null)
                    return type;
            }
            return null;
        }
        
    }

}
