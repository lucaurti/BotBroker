using System.Collections.Generic;

namespace Broker.Common.WebAPI.Cobinhood.OrderBooks
{

    internal class Orderbook
    {
        public int sequence { get; set; }
        public IList<IList<string>> bids { get; set; }
        public IList<IList<string>> asks { get; set; }
    }

    internal class Result
    {
        public Orderbook orderbook { get; set; }
    }

    internal class OrderBook
    {
        public bool success { get; set; }
        public Result result { get; set; }
    }

}
