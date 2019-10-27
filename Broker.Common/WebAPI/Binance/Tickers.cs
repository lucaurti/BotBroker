namespace Broker.Common.WebAPI.Binance
{

    internal class Ticker
    {
        public string symbol { get; set; }
        public string bidPrice { get; set; }
        public string bidQty { get; set; }
        public string askPrice { get; set; }
        public string askQty { get; set; }
    }

}
