namespace Broker.Common.WebAPI.Cobinhood.Orders
{

    internal class Order2
    {
        public string id { get; set; }
        public string trading_pair_id { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string filled { get; set; }
        public string state { get; set; }
        public long timestamp { get; set; }
        public string eq_price { get; set; }
        public string completed_at { get; set; }
        public string source { get; set; }
    }

    internal class Result
    {
        public Order2 order { get; set; }
    }

    internal class Order
    {
        public bool success { get; set; }
        public Result result { get; set; }
    }

}
