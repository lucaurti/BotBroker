using Newtonsoft.Json;

namespace Broker.Common.WebAPI.Cobinhood.Tickers
{

    internal class Ticker
    {
        public string trading_pair_id { get; set; }
        public long timestamp { get; set; }
        public string last_trade_price { get; set; }
        public string highest_bid { get; set; }
        public string lowest_ask { get; set; }


        [JsonProperty("24h_high")]
        public string high { get; set; }
        
        [JsonProperty("24h_low")]
        public string low { get; set; }
        
        [JsonProperty("24h_open")]
        public string open { get; set; }

        [JsonProperty("24h_volume")]
        public string volume { get; set; }
    }

    internal class Result
    {
        public Ticker ticker { get; set; }
    }

    internal class Tickers
    {
        public bool success { get; set; }
        public Result result { get; set; }
    }

}
