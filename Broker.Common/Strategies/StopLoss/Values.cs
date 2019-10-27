using Broker.Common.Utility;
using System;

namespace Broker.Common.Strategies.StopLoss
{

    [Serializable]
    internal class Values
    {

        // variables
        private decimal previousActionPrice = 0;
        private ActionType previousAction = ActionType.None;
        private DateTime lastOpDate = DateTime.Now;
        private MarketType marketState = MarketType.None;


        // properties
        internal decimal BuyAt { get; set; } = 0;
        internal decimal StopLoss { get; set; } = 0;
        internal decimal CurrentCandleClose { get; set; } = 0;
        internal decimal PreviousCandleClose { get; set; } = 0;
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
                if (value == ActionType.Normal || value == ActionType.StopExit)
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
        internal MarketType MarketState 
        { 
            get
            {
                return marketState;
            } 
            set
            {
                marketState = value;
                if (value != MarketType.None)
                    Misc.CacheManager("MarketState", Misc.CacheType.Save, value);
            }
        }

        // enum
        internal enum ActionType
        {
            None,
            WarmUp,
            Normal,
            StopLoss,
            StopExit,
            SospBuy,
            SospLoss,
            Pause
        }
        internal enum MarketType
        {
            None,
            Bullish,
            Bearish
        }


        // function
        internal static ActionType ToActionType(string action)
        {
            if (action == null) 
                return ActionType.None;
            switch (action.ToLower())
            {
                case "warmup": return ActionType.WarmUp;
                case "normal": return ActionType.Normal;
                case "sospbuy": return ActionType.SospBuy;
                case "stoploss": return ActionType.StopLoss;
                case "sosploss": return ActionType.SospLoss;
                case "stopexit": return ActionType.StopExit;
                default: return ActionType.None; //none
            }
        }
        internal static MarketType ToMarketType(string action)
        {
            if (action == null) 
                return MarketType.None;
            switch (action.ToLower())
            {
                case "bullish": return MarketType.Bullish;
                case "bearish": return MarketType.Bearish;
                default: return MarketType.None; //none
            }
        }

    }

}
