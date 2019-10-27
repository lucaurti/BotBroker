namespace Broker.Common.WebAPI.Coinbase
{

    internal class Order
    {
        public string id { get; set; }
        public string size { get; set; }
        public string product_id { get; set; }
        public string side { get; set; }
        public string stp { get; set; }
        public string funds { get; set; }
        public string specified_funds { get; set; }
        public string type { get; set; }
        public bool post_only { get; set; }
        public string created_at { get; set; }
        public string done_at { get; set; }
        public string done_reason { get; set; }
        public string fill_fees { get; set; }
        public string filled_size { get; set; }
        public string executed_value { get; set; }
        public string status { get; set; }
        public bool settled { get; set; }
    }

}
