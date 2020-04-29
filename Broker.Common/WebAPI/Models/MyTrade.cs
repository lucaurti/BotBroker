using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI.Models
{
    public class MyTrade
    {
        public int id { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string quoteQty { get; set; }
        public long time { get; set; }
        public TradeAction action { get; set; }
        public bool isBestMatch { get; set; }
    }
}
