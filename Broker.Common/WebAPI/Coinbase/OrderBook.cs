namespace Broker.Common.WebAPI.Coinbase
{

    internal class OrderBook
    {
        public string time { get; set; }
        public int trade_id { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string side { get; set; }
    }

}
