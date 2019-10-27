using System;
using System.Linq;

namespace Broker.Common.Indicators
{

    public class SMAAverage
    {
        private int tickcount, periods;
        private decimal emav;
        private decimal[] price;

        public SMAAverage(int pPeriods)
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
                emav = price.Sum() / periods;
            else
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