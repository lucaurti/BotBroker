namespace Broker.Common.WebAPI.Coinbase
{

    internal class Balance
    {
        public string id { get; set; }
        public string currency { get; set; }
        public string balance { get; set; }
        public string available { get; set; }
        public string hold { get; set; }
        public string profile_id { get; set; }
    }

}
