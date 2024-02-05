namespace ChainflipInsights.Feeders.CexMovement
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Net.Mime;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using ChainflipInsights.Configuration;
    using ChainflipInsights.Infrastructure;
    using ChainflipInsights.Infrastructure.Pipelines;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class CexMovementFeeder
    {
        private readonly ILogger<CexMovementFeeder> _logger;
        private readonly Pipeline<CexMovementInfo> _pipeline;
        private readonly BotConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public CexMovementFeeder(
            ILogger<CexMovementFeeder> logger,
            IOptions<BotConfiguration> options,
            IHttpClientFactory httpClientFactory,
            Pipeline<CexMovementInfo> pipeline)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = options.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }
        
        public async Task Start()
        {
            if (!_configuration.EnableCexMovement.Value)
            {
                _logger.LogInformation(
                    "CexMovement not enabled. Skipping {TaskName}",
                    nameof(CexMovementFeeder));
                
                return;
            }
            
            _logger.LogInformation(
                "Starting {TaskName}",
                nameof(CexMovementFeeder));

            // Give the consumers some time to connect
            await Task.Delay(_configuration.FeedingDelay.Value, _pipeline.CancellationToken);
            
            // Start a loop fetching CexMovement Info
            await ProvideCexMovementInfo(_pipeline.CancellationToken);
            
            _logger.LogInformation(
                "Stopping {TaskName}",
                nameof(CexMovementFeeder));
        }

        private async Task ProvideCexMovementInfo(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            var lastDay = await GetLastCexMovementDay(cancellationToken);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                
                var cexMovement = await GetCexMovement(lastDay, cancellationToken);
                
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (cexMovement == null)
                {
                    await Task.Delay(_configuration.CexMovementQueryDelay.Value.RandomizeTime(), cancellationToken);
                    continue;                    
                }
                
                var cexMovementPerDay = cexMovement
                    .Data.Data
                    .OrderBy(x => x.DayOfYear)
                    .ToList();
                
                if (cexMovementPerDay.Count <= 0)
                {
                    _logger.LogInformation(
                        "No new CEX movement to announce. Last CEX movement day is still {CexMovementDay}",
                        lastDay);
                }

                // CEX Movement is in increasing order
                foreach (var cexMovementDay in cexMovementPerDay.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
                {
                    var cexMovementInfo = new CexMovementInfo(cexMovementDay);

                    // TODO: Add logic here to only broadcast current day once

                }
                
                await Task.Delay(_configuration.FundingQueryDelay.Value.RandomizeTime(), cancellationToken);
            }
        }
        
        private async Task<int> GetLastCexMovementDay(CancellationToken cancellationToken)
        {
            if (File.Exists(_configuration.LastCexMovementDayLocation))
                return int.Parse(await File.ReadAllTextAsync(_configuration.LastCexMovementDayLocation, cancellationToken));
            
            await using var file = File.CreateText(_configuration.LastCexMovementDayLocation);
            await file.WriteAsync("32");
            return 32;
        }
        
        private async Task StoreLastCexMovementDay(int cexMovementDay)
        {
            await using var file = File.CreateText(_configuration.LastCexMovementDayLocation);
            await file.WriteAsync(cexMovementDay.ToString(CultureInfo.InvariantCulture));
        }
        
        private async Task<CexMovementResponse?> GetCexMovement(
            double fromId,
            CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("Dune");

            return await client.GetFromJsonAsync<CexMovementResponse>(
                $"{_configuration.DuneUrl}{_configuration.DuneCexMovementQuery}/results?api_key={_configuration.DuneApiKey}",
                cancellationToken);
        }
    }
}