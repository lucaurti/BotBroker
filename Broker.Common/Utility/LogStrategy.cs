using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Serilog.Events;
using Serilog;

namespace Broker.Common.Utility
{
    public class LogStrategy
    {        
        public enum Destination
        {
            File,
            Telegram,
            All
        }
        private static Dictionary<int, string> logOnCandleMessage;

        public static string GetLog()
        {
            StringBuilder sb = new StringBuilder();
            if (logOnCandleMessage!=null)
            {
                foreach (var item in logOnCandleMessage.OrderBy(a=>a.Key))
                {
                    sb.AppendLine(item.Value);
                }
                logOnCandleMessage = null;
                return sb.ToString();
            }
            else
                return "";
        }

        public static void AppendLog(string arg, LogEventLevel level = LogEventLevel.Information, Destination dest = Destination.File)
        {
            switch (dest)
            {
                case Destination.File:
                    LogOnFile(arg, level);
                    break;
                case Destination.Telegram:
                    LogOnTelegram(arg);
                    break;
                case Destination.All:
                    LogOnFile(arg, level);
                    LogOnTelegram(arg);
                    break;
            }  
        }

        private static void LogOnTelegram(string arg)
        {
            if (logOnCandleMessage==null)
            {
                logOnCandleMessage = new Dictionary<int, string>();
                logOnCandleMessage.Add(0, "Candle: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            }
            else
            {   
                int index = logOnCandleMessage.Max(a=>a.Key)+1;
                logOnCandleMessage.Add(index, arg);
            }
        }

        private static void LogOnFile(string arg, LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Debug:
                    Log.Debug(arg);
                    break;
                case LogEventLevel.Information:
                    Log.Information(arg);
                    break;
                case LogEventLevel.Verbose:
                    Log.Verbose(arg);
                    break;
                case LogEventLevel.Warning:
                    Log.Warning(arg);
                    break;
                case LogEventLevel.Fatal:
                    Log.Fatal(arg);
                    break;
                case LogEventLevel.Error:
                    Log.Error(arg);
                    break;
            }
        }
    }
}