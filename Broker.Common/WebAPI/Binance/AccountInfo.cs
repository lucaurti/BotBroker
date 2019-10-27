using System.Collections.Generic;
namespace Broker.Common.WebAPI.Binance
{

    internal class Balance
    {
        public string asset { get; set; }
        public string free { get; set; }
        public string locked { get; set; }
    }

    internal class AccountInfo
    {
        public int makerCommission { get; set; }
        public int takerCommission { get; set; }
        public int buyerCommission { get; set; }
        public int sellerCommission { get; set; }
        public bool canTrade { get; set; }
        public bool canWithdraw { get; set; }
        public bool canDeposit { get; set; }
        public long updateTime { get; set; }
        public IList<Balance> balances { get; set; }
    }

}
