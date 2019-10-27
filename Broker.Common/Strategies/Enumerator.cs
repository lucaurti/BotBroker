namespace Broker.Common.Strategies
{
    public class Enumerator
    {

        public enum CandleType
        {
            Low,
            High,
            Close,
            Open
        }
        public enum TradeAction
        {
            Short,
            Long
        }
        public enum TradeState
        {
            Pending,
            Completed
        }
        public enum TradeStatus
        {
            Completed,
            Cancelled,
            Errored,
            Aborted
        }
        public enum TypeCoin 
        {
            Asset,
            Currency
        }

    }
}
