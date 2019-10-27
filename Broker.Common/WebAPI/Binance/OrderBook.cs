using System.Collections.Generic;

namespace Broker.Common.WebAPI.Binance
{

    internal class OrderBooks
    {
        public int lastUpdateId { get; set; }
        public IList<IList<string>> bids { get; set; }
        public IList<IList<string>> asks { get; set; }
    }

}
