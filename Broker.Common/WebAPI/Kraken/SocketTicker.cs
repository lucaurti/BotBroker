using Broker.Common.WebAPI.Models;

namespace Broker.Common.WebAPI.Kraken
{

    internal class SocketTicker
    {
        public string[] b { get; set; } //bid
        public string[] a { get; set; } //ask
        public string[] c { get; set; } //lastTrade [<price>, <lot volume>]
        public string[] v { get; set; } //volume array[<today>, <last 24 hours>]
        public string[] o { get; set; } //today's opening price
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

        public static string ToCorrectJson(MyWebAPISettings settings, string json)
        {
            json = json.Replace("\"","'");
            int first = json.IndexOf("{");
            int last = json.LastIndexOf("}");
            var s = json.Substring(first,last-first+1);
            return s;
        }
    }

}
