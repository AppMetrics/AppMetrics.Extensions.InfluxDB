﻿// <copyright file="InfluxDbReporterExtensions.cs" company="Allan Hardy">
// Copyright (c) Allan Hardy. All rights reserved.
// </copyright>

using System;
using App.Metrics.Abstractions.Filtering;
using App.Metrics.Extensions.Reporting.InfluxDB;
using App.Metrics.Extensions.Reporting.InfluxDB.Client;
using App.Metrics.Reporting.Abstractions;

// ReSharper disable CheckNamespace
namespace App.Metrics.Reporting.Interfaces
{
    // ReSharper restore CheckNamespace
    public static class InfluxDbReporterExtensions
    {
        public static IReportFactory AddInfluxDb(
            this IReportFactory factory,
            InfluxDBReporterSettings settings,
            IFilterMetrics filter = null)
        {
            factory.AddProvider(new InfluxDbReporterProvider(settings, filter));
            return factory;
        }

        public static IReportFactory AddInfluxDb(
            this IReportFactory factory,
            string database,
            Uri baseAddress,
            IFilterMetrics filter = null)
        {
            var settings = new InfluxDBReporterSettings
                           {
                               InfluxDbSettings = new InfluxDBSettings(database, baseAddress)
                           };

            factory.AddInfluxDb(settings, filter);
            return factory;
        }
    }
}