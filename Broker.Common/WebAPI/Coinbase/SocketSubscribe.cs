using System.Collections.Generic;

namespace Broker.Common.WebAPI.Coinbase
{

    internal class SocketSubscribe
    {
        public string type { get; set; }
        public IList<string> product_ids { get; set; }
        public IList<object> channels { get; set; }
    }

}
