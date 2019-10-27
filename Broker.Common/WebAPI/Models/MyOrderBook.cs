using System;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI.Models
{

    public class MyOrderBook
    {
        public UInt64 Timestamp { get; set; }
        public decimal Volume { get; set; }
        public decimal Price { get; set; }
        public TradeAction Action { get; set; }
        public MyWebAPISettings Settings { get; set; }

    }

}
