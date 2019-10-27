using Broker.Common.WebAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.Utility
{
    public class BrokerDBContext : DbContext
    {
        public DbSet<MyTicker> MyTickers { get; set; }
        public DbSet<MyCandle> MyCandles { get; set; }
        public DbSet<MyOrder> MyOrders { get; set; }
        public DbSet<MyBalance> MyBalances { get; set; }
        public DbSet<MySetup> MySetups { get; set; }
        public DbSet<MyMACD> MyMACDs { get; set; }
        public DbSet<MyMomentum> MyMomentums { get; set; }
        public DbSet<MyRSI> MyRSIs { get; set; }
        public DbSet<MyWebAPISettings> MyWebAPISettings { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=" + ConfigurationManager.GetPathDatabaseFile);
            optionsBuilder.EnableSensitiveDataLogging();
            //optionsBuilder.UseSqlite("Data Source=Broker.db");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

        }

    }

    public class MyTicker
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public long Timestamp { get; set; }
        public decimal Ask { get; set; }
        public decimal Bid { get; set; }
        public decimal Volume { get; set; }
        public decimal LastTrade { get; set; }
        public MyWebAPISettings Settings { get; set; }

    }

    public class MySetup
    {
        [Key]
        public string Key { get; set; }
        public string Value { get; set; }

    }

    public class MyMACD
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public long Timestamp { get; set; }
        public decimal FastValue { get; set; }
        public decimal SlowValue { get; set; }
        public decimal SignalValue { get; set; }
        public decimal MACD { get; set; }
        public decimal Hist { get; set; }
        public MyCandle Candle { get; set; }

    }

    public class MyMomentum
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public long Timestamp { get; set; }
        public decimal MomentumValue { get; set; }
        public MyCandle Candle { get; set; }

    }

    public class MyRSI
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public long Timestamp { get; set; }
        public decimal RSIValue { get; set; }
        public MyCandle Candle { get; set; }

    }

    public class MyCandle
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public MyWebAPISettings Settings { get; set; }

    }

    public class MyOrder
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string OrderId { get; set; }
        public long Creation { get; set; }
        public long Completed { get; set; }
        public TradeAction Type { get; set; }
        public TradeState State { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public decimal Fee { get; set; }
        public MyWebAPISettings Settings { get; set; }
    }

    public class MyBalance
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public decimal Reserved { get; set; }
        public decimal ToEuro { get; set; }
        public MyWebAPISettings Settings { get; set; }
        public MyCandle Candle { get; set; }
    }

}