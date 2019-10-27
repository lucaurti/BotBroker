using Broker.Common.Utility;
using System;

namespace Broker.Common.Strategies.Momentum
{

    [Serializable]
    internal class Values
    {

        // variables
        private decimal previousActionPrice = 0;
        private ActionType previousAction = ActionType.None;
        private DateTime lastOpDate = DateTime.Now;


        // properties
        internal decimal StopLoss { get; set; } = 0;
        internal decimal CurrentCandleClose { get; set; } = 0;
        internal decimal PreviousCandleClose { get; set; } = 0;
        internal decimal? LimitShortPrice { get; set; } = null;
        internal decimal LastHigh { get; set; } = 0;
        internal decimal LastLow { get; set; } = decimal.MaxValue;


        // saved properties
        internal decimal PreviousActionPrice 
        { 
            get 
            { 
                return previousActionPrice; 
            }
            set 
            {
                previousActionPrice = value;
                Misc.CacheManager("PreviousActionPrice", Misc.CacheType.Save, value);
            }
        }
        internal ActionType PreviousAction 
        { 
            get
            {
                return previousAction;
            } 
            set
            {
                previousAction = value;
                if (value == ActionType.Sell || value == ActionType.Buy)
                    Misc.CacheManager("PreviousAction", Misc.CacheType.Save, value);
                if (value == ActionType.Pause)
                    Misc.CacheManager("BotInPause", Misc.CacheType.Save, true);
                else
                    Misc.CacheManager("BotInPause", Misc.CacheType.Save, false);
            }
        }
        internal DateTime LastOpDate 
        { 
            get
            {
                return lastOpDate;
            } 
            set
            {
                lastOpDate = value;
                Misc.CacheManager("LastOpDate", Misc.CacheType.Save, value);
            }
        }


        // enum
        internal enum ActionType
        {
            None,
            WarmUp,
            Sell,
            Buy,
            StopLoss,
            StopExit,
            SospBuy,
            SospSell,
            SospLoss,
            Pause
        }


        // function
        internal static ActionType ToActionType(string action)
        {
            if (action == null) 
                return ActionType.None;
            switch (action.ToLower())
            {
                case "warmup": return ActionType.WarmUp;
                case "sell": return ActionType.Sell;
                case "buy": return ActionType.Buy;
                case "sospbuy": return ActionType.SospBuy;
                case "sospdell": return ActionType.SospSell;
                case "stoploss": return ActionType.StopLoss;
                case "sosploss": return ActionType.SospLoss;
                case "stopexit": return ActionType.StopExit;
                default: return ActionType.None; //none
            }
        }

    }

}
