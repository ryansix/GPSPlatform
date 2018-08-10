﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperSocket.SocketBase.Logging;
using GPS.JT808PubSubToKafka;
using GPS.PubSub.Abstractions;

namespace GPS.Gateway.JT808SuperSocketServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var serverHostBuilder = new HostBuilder()
                            .ConfigureHostConfiguration((config) =>
                            {
                                config.AddEnvironmentVariables();
                            })
                            .ConfigureAppConfiguration((hostingContext, config) =>
                            {
                                config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                      .AddJsonFile($"appsettings.{ hostingContext.HostingEnvironment}.json", optional: true, reloadOnChange: true);
                                config.AddEnvironmentVariables();
                            })
                            .ConfigureLogging((context, logging) =>
                            {
                                NLog.LogManager.LoadConfiguration("Configs/nlog.config");
                                logging.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
                                logging.SetMinimumLevel(LogLevel.Trace);
                            })
                            .ConfigureServices((hostContext, services) =>
                            {
                                services.AddSingleton<ILoggerFactory, LoggerFactory>();
                                services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
                                var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
                                services.AddSingleton<JT808MsgIdHandler>();
                                services.AddSingleton<ILogFactory, SuperSocketNLogFactoryExtensions>();
                                var host = hostContext.Configuration.GetSection("KafkaOptions").GetValue<string>("bootstrap.servers");
                                KafkaMonoConfig.Load(hostContext.Configuration.GetSection("KafkaOptions").GetValue<string>("MonoRuntimePath"));
                                services.Configure<SuperSocketOptions>(hostContext.Configuration.GetSection("SuperSocketOptions"));
                                services.AddSingleton(typeof(IProducerFactory), 
                                    new JT808MsgIdProducerFactory(
                                        new GPS.JT808PubSubToKafka.JT808_0x0200_Producer(
                                            new Dictionary<string, object>
                                            {
                                                { "bootstrap.servers", host }
                                            })
                                ));
                                services.AddSingleton(typeof(IConsumerFactory),
                                    new JT808MsgIdConsumerFactory(
                                        new GPS.JT808PubSubToKafka.JT808_UnificationSend_Consumer(
                                            new Dictionary<string, object>
                                            {
                                                { "group.id", "GatewayUnificationSend" },
                                                { "enable.auto.commit", true },
                                                { "bootstrap.servers", host }
                                            }, loggerFactory)));
                                services.AddSingleton<JT808Server>();
                                services.AddScoped<IHostedService, JT808Service>();
                            });
                await serverHostBuilder.RunConsoleAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:" + ex.ToString());
            }
        }
    }
}
