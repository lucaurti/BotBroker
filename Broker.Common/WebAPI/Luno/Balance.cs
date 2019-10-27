using System.Collections.Generic;

namespace Broker.Common.WebAPI.Luno
{

    internal class BalanceItem
    {
        public string account_id { get; set; }
        public string asset { get; set; }
        public string balance { get; set; }
        public string reserved { get; set; }
        public string unconfirmed { get; set; }
        public string name { get; set; }
    }

    internal class Balance
    {
        public IList<BalanceItem> balance { get; set; }
    }

}
