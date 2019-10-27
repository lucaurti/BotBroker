using System.Collections.Generic;

namespace Broker.Common.WebAPI.Cobinhood.Balances
{

    internal class Balance
    {
        public string currency { get; set; }
        public string type { get; set; }
        public string total { get; set; }
        public string on_order { get; set; }
        public bool locked { get; set; }
        public string usd_value { get; set; }
        public string btc_value { get; set; }
    }

    internal class Result
    {
        public IList<Balance> balances { get; set; }
    }

    internal class Balances
    {
        public bool success { get; set; }
        public Result result { get; set; }
    }

}
