using System.Collections.Generic;

namespace Broker.Common.WebAPI.Luno
{

    internal class Ask
    {
        public string price { get; set; }
        public string volume { get; set; }
    }

    internal class Bid
    {
        public string price { get; set; }
        public string volume { get; set; }
    }

    internal class OrderBooks
    {
        public long timestamp { get; set; }
        public IList<Ask> asks { get; set; }
        public IList<Bid> bids { get; set; }
    }

}
