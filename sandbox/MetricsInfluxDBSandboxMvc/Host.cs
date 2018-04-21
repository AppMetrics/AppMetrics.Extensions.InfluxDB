﻿// <copyright file="Host.cs" company="Allan Hardy">
// Copyright (c) Allan Hardy. All rights reserved.
// </copyright>

using System.IO;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.InfluxDB;
using App.Metrics.Reporting.InfluxDB;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace MetricsInfluxDBSandboxMvc
{
    public static class Host
    {
        public static IWebHost BuildWebHost(string[] args)
        {
            ConfigureLogging();

            var configuration = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
                                .Build();

            var influxOptions = new MetricsReportingInfluxDbOptions();
            configuration.GetSection(nameof(MetricsReportingInfluxDbOptions)).Bind(influxOptions);

            influxOptions.MetricsOutputFormatter = new MetricsInfluxDbLineProtocolOutputFormatter();

            return WebHost.CreateDefaultBuilder(args)
                          .ConfigureMetricsWithDefaults(
                              builder =>
                              {
                                  builder.Report.ToInfluxDb(influxOptions);
                              })
                          .UseMetrics()
                          .UseSerilog()
                          .UseStartup<Startup>()
                          .Build();
        }

        public static void Main(string[] args) { BuildWebHost(args).Run(); }

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
                .WriteTo.LiterateConsole()
                .WriteTo.Seq("http://localhost:5341")
                .CreateLogger();
        }
    }
}