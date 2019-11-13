using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broker.Common.Events;
using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Broker.Batch
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // culture
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            string pathExposed = Path.Combine(AppContext.BaseDirectory, "Exposed");

            // copy database and configuration if needed
            if (!Directory.Exists(pathExposed))
                Directory.CreateDirectory(pathExposed);
            if (!File.Exists(Path.Combine(pathExposed, "appsettings.json")))
            {
                File.Copy(
                    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                    Path.Combine(pathExposed, "appsettings.json"));
            }
            if (!File.Exists(Path.Combine(pathExposed, "broker.db")))
            {
                File.Copy(
                    Path.Combine(AppContext.BaseDirectory, "Broker.db"),
                    Path.Combine(pathExposed, "broker.db"));
            }

            // configuation
            var config = new ConfigurationManager(ConfigurationManager.GetConfiguration);
            string apiExchange = config.GetParameterValue("apiExchange", "services");
            string apiStrategy = config.GetParameterValue("apiStrategy", "services");
            List<Task> listTask = new List<Task>();

            // logger
            LoggerConfiguration loggerConfiguration =
                new LoggerConfiguration()
                .Filter.ByExcluding(s => s.MessageTemplate.Text.Contains("WEBSOCKET CLIENT"))
                .WriteTo.Console()
                .WriteTo.Logger(l => l.Filter.ByExcluding(e => e.Level == LogEventLevel.Fatal || e.Level == LogEventLevel.Verbose)
                                        .WriteTo.File(Path.Combine(pathExposed, "broker.log")))
                .WriteTo.Logger(l => l.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Fatal)
                                        .WriteTo.File(Path.Combine(pathExposed, "brokerStrategy.log")));

            if (config.isDebugLog) loggerConfiguration.MinimumLevel.Debug();
            else loggerConfiguration.MinimumLevel.Information();
            Log.Logger = loggerConfiguration.CreateLogger();

            // batch
            if (config.mustStartBatch)
            {
                var builderB = new HostBuilder()
                    .ConfigureServices((hostContext, services) =>
                        {
                            // standard injection
                            services.AddSingleton<IConfigurationManager>(config);
                            services.AddSingleton<IEvents, MyEvents>();
                            services.AddSingleton<IWebSocket, MyWebSocket>();
                            services.AddSingleton<MyStrategy>();

                            // parameter injection
                            services.Add(
                                new ServiceDescriptor(
                                    serviceType: typeof(IWebAPI),
                                    implementationType: apiExchange.ToType("Broker.Common"),
                                    lifetime: ServiceLifetime.Singleton
                                )
                            );
                            services.Add(
                                new ServiceDescriptor(
                                    serviceType: typeof(IStrategy),
                                    implementationType: apiStrategy.ToType("Broker.Common"),
                                    lifetime: ServiceLifetime.Singleton
                                )
                            );

                            // multicoin
                            services.AddSingleton<IList<MyWebAPI>>(cnx =>
                           {
                               IList<MyWebAPI> result = new List<MyWebAPI>();
                               var webapi = cnx.GetService<IWebAPI>();
                               var events = cnx.GetService<IEvents>();
                               var socket = cnx.GetService<IWebSocket>();

                               string app = config.GetParameterValue("multicoin", Misc.GetStrategy);
                               bool isMultiCoinStrategy = bool.Parse(app ?? "false");
                               List<string> parameterList = config.GetParameterValueList("multicoin", Misc.GetExchange);
                               if (parameterList.Count > 0)
                               {
                                   if (isMultiCoinStrategy)
                                       foreach (string index in parameterList)
                                           result.Add(
                                               new MyWebAPI(
                                                   Misc.GenerateMyWebAPISettings(int.Parse(index)), webapi, events, socket));
                                   else
                                       result.Add(
                                           new MyWebAPI(
                                               Misc.GenerateMyWebAPISettings(0), webapi, events, socket));
                               }
                               else
                                   result.Add(
                                       new MyWebAPI(
                                           Misc.GenerateMyWebAPISettings(), webapi, events, socket));
                               return result;
                           }
                            );

                            // services locator
                            ServiceLocator.SetLocatorProvider(services.BuildServiceProvider());

                            // hosted service
                            services.AddHostedService<StartupB>();
                        })
                    .ConfigureLogging(
                        (hostContext, services) => services.AddSerilog(dispose: true)
                    );
                listTask.Add(builderB.RunConsoleAsync());
            }

            // web
            if (config.mustStartWeb)
            {
                var builderW = WebHost.CreateDefaultBuilder(args)
                    //.UseSerilog(Log.Logger)
                    .UseKestrel()
                    .UseSetting("detailedErrors", "true")
                    .CaptureStartupErrors(true)
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseStartup<StartupW>().Build();
                listTask.Add(builderW.RunAsync());
            }

            // log init
            Log.Information("-> Batch active     : " + config.mustStartBatch.ToString());
            if (config.mustStartBatch)
            {
                Log.Information("-> Paper trade      : " + Misc.IsPaperTrade.ToString());
                Log.Information("-> WebSocket active : " + Extension.UseWebSocketTickers.ToString());
                Log.Information("-> Telegram active  : " + config.mustStartTelegram.ToString());
            }
            Log.Information("-> Web active       : " + config.mustStartWeb.ToString());
            Log.Information("-> Exchange         : " + apiExchange);
            Log.Information("-> Strategy         : " + apiStrategy);

            Log.Information("-> PathExposed: " + pathExposed);
            Log.Information("-> BaseDirectory: " + AppContext.BaseDirectory);

            // wait task completition
            await Task.WhenAll(listTask.ToArray());
        }

    }
}
