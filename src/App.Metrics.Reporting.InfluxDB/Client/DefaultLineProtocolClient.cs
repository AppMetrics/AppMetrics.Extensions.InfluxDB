// <copyright file="DefaultLineProtocolClient.cs" company="Allan Hardy">
// Copyright (c) Allan Hardy. All rights reserved.
// </copyright>

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace App.Metrics.Reporting.InfluxDB.Client
{
    public class DefaultLineProtocolClient : ILineProtocolClient
    {
        private static readonly ILog Logger = LogProvider.For<DefaultLineProtocolClient>();

        private readonly HttpClient _httpClient;
        private readonly InfluxDbOptions _influxDbOptions;
        private readonly Policy<LineProtocolWriteResult> _executionPolicy;

        public DefaultLineProtocolClient(
            InfluxDbOptions influxDbOptions,
            HttpPolicy httpPolicy,
            HttpClient httpClient)
        {
            _influxDbOptions = influxDbOptions ?? throw new ArgumentNullException(nameof(influxDbOptions));
            _httpClient = httpClient;
            var backOffPeriod = httpPolicy?.BackoffPeriod ?? throw new ArgumentNullException(nameof(httpPolicy));
            var failuresBeforeBackoff = httpPolicy.FailuresBeforeBackoff;

            var circutBreaker = Policy<LineProtocolWriteResult>
                .Handle<Exception>()
                .OrResult(result => !result.Success)
                .CircuitBreakerAsync(failuresBeforeBackoff, backOffPeriod);
            _executionPolicy = Policy<LineProtocolWriteResult>
                .Handle<BrokenCircuitException>()
                .FallbackAsync(LineProtocolWriteResult.Error("Too many failures in writing to InfluxDB, Circuit Opened"))
                .WrapAsync(circutBreaker);
        }

        public Task<LineProtocolWriteResult> WriteAsync(
            string payload,
            CancellationToken cancellationToken = default)
        {
            return _executionPolicy.ExecuteAsync(
                async (cancelation) =>
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return LineProtocolWriteResult.Ok();
                }
                try
                {
                    var content = new StringContent(payload, Encoding.UTF8);

                    var response = await _httpClient.PostAsync(_influxDbOptions.Endpoint, content, cancelation);

                    if (response.StatusCode == HttpStatusCode.NotFound && _influxDbOptions.CreateDataBaseIfNotExists)
                    {
                        await TryCreateDatabase(cancelation);

                        response = await _httpClient.PostAsync(_influxDbOptions.Endpoint, content, cancelation);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMessage = $"Failed to write to InfluxDB - StatusCode: {response.StatusCode} Reason: {response.ReasonPhrase}";
                        Logger.Error(errorMessage);

                        return LineProtocolWriteResult.Error(errorMessage);
                    }

                    Logger.Trace("Successful write to InfluxDB");

                    return LineProtocolWriteResult.Ok();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to write to InfluxDB");
                    throw;
                }
            }, cancellationToken);
        }

        private Task<LineProtocolWriteResult> TryCreateDatabase(CancellationToken cancellationToken = default)
        {
            return _executionPolicy.ExecuteAsync(
                async (token) =>
                {
                    try
                    {
                        Logger.Trace($"Attempting to create InfluxDB Database '{_influxDbOptions.Database}'");

                        var content = new StringContent(string.Empty, Encoding.UTF8);

                    var response = await _httpClient.PostAsync($"query?q=CREATE DATABASE \"{Uri.EscapeDataString(_influxDbOptions.Database)}\"", content, token);

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorMessage = $"Failed to create InfluxDB Database '{_influxDbOptions.Database}' - StatusCode: {response.StatusCode} Reason: {response.ReasonPhrase}";
                            Logger.Error(errorMessage);

                            return LineProtocolWriteResult.Error(errorMessage);
                        }

                        Logger.Trace($"Successfully created InfluxDB Database '{_influxDbOptions.Database}'");

                        return LineProtocolWriteResult.Ok();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to create InfluxDB Database'{_influxDbOptions.Database}'");
                        throw;
                    }
                }, cancellationToken);
        }
    }
}
