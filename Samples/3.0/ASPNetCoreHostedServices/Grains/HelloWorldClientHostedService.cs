﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;

namespace AspNetCoreHostedServices.Grains
{
    public class HelloWorldClientHostedService : IHostedService
    {
        public IClusterClient Client { get; }
        private readonly ILogger<HelloWorldClientHostedService> _logger;

        public HelloWorldClientHostedService(ILogger<HelloWorldClientHostedService> logger,
            ILoggerProvider loggerProvider)
        {
            _logger = logger;
            Client = new ClientBuilder()
                .UseLocalhostClustering()
                .Configure<ClusterOptions>(opts =>
                {
                    opts.ClusterId = "dev";
                    opts.ServiceId = "HellowWorldAPIService";
                })
                .ConfigureLogging(builder => builder.AddProvider(loggerProvider))
                .Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var attempt = 0;
            var maxAttempts = 100;
            var delay = TimeSpan.FromSeconds(1);
            return Client.Connect(async error =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                if (++attempt < maxAttempts)
                {
                    _logger.LogWarning(error,
                        "Failed to connect to Orleans cluster on attempt {@Attempt} of {@MaxAttempts}.",
                        attempt, maxAttempts);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }

                    return true;
                }
                else
                {
                    _logger.LogError(error,
                        "Failed to connect to Orleans cluster on attempt {@Attempt} of {@MaxAttempts}.",
                        attempt, maxAttempts);

                    return false;
                }
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Client.Close();
            }
            catch (OrleansException error)
            {
                _logger.LogWarning(error,
                    "Error while gracefully disconnecting from Orleans cluster. Will ignore and continue to shutdown.");
            }
        }
    }
}