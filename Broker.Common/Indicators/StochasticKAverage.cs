using System;
using System.Linq;

namespace Broker.Common.Indicators
{

    public class StochasticKAverage
    {
        private int tickcount, periods;
        private decimal emav;
        private decimal[] price;

        public StochasticKAverage(int pPeriods)
        {
            periods = pPeriods;
            price = new decimal[pPeriods];
        }

        public void ReceiveTick(decimal Val)
        {
            var newArray = new decimal[periods];
            int i = periods - 1;
            Array.Copy(price, 1, newArray, 0, price.Length - 1);
            newArray[i] = Val; price = newArray;
            if (tickcount > periods) 
            {
                decimal lowest = price.Min();
                decimal highest = price.Max();
                decimal diff = highest - lowest;
                emav = (diff > 0) ? (100 * (Val - lowest) / diff) : 0;
            }
            if (tickcount <= (periods + 1))
                tickcount++;
        }
        public decimal Value()
        {
            return isPrimed() ? emav : 0;
        }
        public bool isPrimed()
        {    
            return tickcount > periods;
        }
        public int PeriodElaborated() 
        {
            return tickcount;
        }

    }

}