using System.Collections.Generic;

namespace Broker.Common.WebAPI.Luno
{

    internal class Ticker
    {
        public object timestamp { get; set; }
        public string bid { get; set; }
        public string ask { get; set; }
        public string last_trade { get; set; }
        public string rolling_24_hour_volume { get; set; }
        public string pair { get; set; }
    }

    internal class Tickers
    {
        public IList<Ticker> tickers { get; set; }
    }

}