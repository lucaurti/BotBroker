namespace Broker.Common.WebAPI.Coinbase
{

    internal class SocketTicker
    {
        public string type { get; set; }
        public int trade_id { get; set; }
        public long sequence { get; set; }
        public string time { get; set; }
        public string product_id { get; set; }
        public string price { get; set; }
        public string side { get; set; }
        public string last_size { get; set; }
        public string best_bid { get; set; }
        public string best_ask { get; set; }
    }

}
