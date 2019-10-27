using Newtonsoft.Json;

namespace Broker.Common.WebAPI.Luno
{

    internal class Order
    {
        public string order_id { get; set; }
        public object creation_timestamp { get; set; }
        public object expiration_timestamp { get; set; }
        public object completed_timestamp { get; set; }
        public string type { get; set; }
        public string state { get; set; }
        public string limit_price { get; set; }
        public string limit_volume { get; set; }
        public string counter { get; set; }
        public string fee_base { get; set; }
        public string fee_counter { get; set; }

        [JsonProperty("base")]
        public string based { get; set; }
    }

}
