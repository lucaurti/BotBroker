using System.Collections.Generic;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;

namespace Broker.Common.WebAPI.Kraken.Tickers
{

    internal class Ticker
    {
        public List<string> error { get; set; }
        public Result result { get; set; }

        public string ToCorrectJson(MyWebAPISettings settings, string json)
        {
            return json.Replace("\""+settings.Asset + settings.Currency+"\":","\"TickerValue\":");
        }
    }

    internal class Result
    {
        public TickerValue TickerValue { get; set; } //tickers
    }

    internal class TickerValue
    {
        public string[] b { get; set; } //bid
        public string[] a { get; set; } //ask
        public string[] c { get; set; } //lastTrade [<price>, <lot volume>]
        public string[] v { get; set; } //volume array[<today>, <last 24 hours>]
        public string o { get; set; } //today's opening price
        public string[] h {get;set;} //high array(<today>, <last 24 hours>),
        public string[] l {get;set;} //low array(<today>, <last 24 hours>),
        public string[] t {get;set;} //number of trades array(<today>, <last 24 hours>),
        public string[] p {get;set;} //volume weighted average price(<today>, <last 24 hours>),
        
        public string Ask
        {
            get { return a[0];}
        }

        public string Bid
        {
            get { return b[0];}
        }

        public string LastTrade
        {
            get { return c[0];}
        }

        public string Volume
        {
            get { return v[0];}
        }
    }
}