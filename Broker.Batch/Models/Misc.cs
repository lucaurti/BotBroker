using System.Linq;
using Broker.Common.Utility;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Batch.Models
{
    public class Utility
    {
        public static decimal CalculateStopLoss(decimal High)
        {
            IConfigurationManager myService = ServiceLocator.Current.GetInstance<IConfigurationManager>();
            var parameter = myService.FindParameter("stoploss", Misc.GetStrategy);
            if (parameter != null)
            {
                var stopmin = myService.FindParameter("stoplossmin", Misc.GetStrategy);
                if (stopmin == null) stopmin = "0";
                else stopmin = myService.GetParameterValue(stopmin, Misc.GetStrategy);
                parameter = myService.GetParameterValue(parameter, Misc.GetStrategy);
                decimal stopLossPerc = High * parameter.ToDecimal();
                decimal stopLossMin = High - stopmin.ToDecimal();
                return (stopLossPerc < stopLossMin ? stopLossPerc : stopLossMin);
            }
            return 0;
        }

        public static decimal CalculateBuyAtUp(out decimal LastBuy, out decimal LastSell)
        {
            LastBuy = 0; LastSell = 0;
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var order = db.MyOrders
                .OrderByDescending(s => s.Completed)
                .FirstOrDefault();
                if (order != null)
                {
                    if (order.Type == TradeAction.Long)
                    {
                        LastBuy = order.Price;
                        return 0;
                    }
                    IConfigurationManager myService = ServiceLocator.Current.GetInstance<IConfigurationManager>();
                    var parameter = myService.FindParameter("buyatup", Misc.GetStrategy);
                    if (parameter != null)
                    {
                        LastSell = order.Price;
                        parameter = myService.GetParameterValue(parameter, Misc.GetStrategy);
                        return order.Price * parameter.ToDecimal();
                    }
                }
                return 0;
            }
        }
    }
}