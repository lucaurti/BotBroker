using System.Collections.Generic;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;

namespace Broker.Common.WebAPI.Kraken.OrdersBook
{
    public class ListOrderBooks
    {
        public List<List<object>> asks { get; set; }
        public List<List<object>> bids { get; set; }
    }

    public class Result
    {
        public ListOrderBooks ListOrderBooks { get; set; }
    }

    public class OrderBook
    {
        public List<object> error { get; set; }
        public Result result { get; set; }

        public string ToCorrectJson(MyWebAPISettings settings, string json)
        {
            return json.Replace("\""+settings.Asset + settings.Currency+"\":","\"ListOrderBooks\":");
        }
    }
}
