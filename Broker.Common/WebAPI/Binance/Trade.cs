using System;
using System.Collections.Generic;

namespace Broker.Common.WebAPI.Binance
{
    public class Trade
    {
        public int id { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string quoteQty { get; set; }
        public long time { get; set; }
        public bool isBuyerMaker { get; set; }
        public bool isBestMatch { get; set; }
    }
}
