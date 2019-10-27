namespace Broker.Common.WebAPI.Binance
{

    internal class CancelOrder
    {
        public string symbol { get; set; }
        public int orderId { get; set; }
        public string origClientOrderId { get; set; }
        public string clientOrderId { get; set; }
        public long transactTime { get; set; }
        public string price { get; set; }
        public string origQty { get; set; }
        public string executedQty { get; set; }
        public string cummulativeQuoteQty { get; set; }
        public string status { get; set; }
        public string timeInForce { get; set; }
        public string type { get; set; }
        public string side { get; set; }
    }

}
