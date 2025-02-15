namespace ChainflipInsights.Feeders.CfeVersion
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
    using Semver;

    public class CfeVersionFeeder : IFeeder
    {
        private const string CfeVersionQuery = 
            """
            {
                allCfeVersions(orderBy: ID_DESC) {
                    edges {
                        node {
                            id
                            validatorsByCfeVersionId {
                                edges {
                                    node {
                                        idSs58
                                        lastHeartbeatBlockId
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        private const string LastBlockQuery =
            """
            {
                allBlocks(orderBy: ID_DESC, first: 1) {
                    nodes {
                        id
                    }
                } 
            }
            """;
        
        private readonly ILogger<CfeVersionFeeder> _logger;
        private readonly Pipeline<CfeVersionsInfo> _pipeline;
        private readonly BotConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public CfeVersionFeeder(
            ILogger<CfeVersionFeeder> logger,
            IOptions<BotConfiguration> options,
            IHttpClientFactory httpClientFactory,
            Pipeline<CfeVersionsInfo> pipeline)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = options.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }
        
        public async Task Start()
        {
            if (!_configuration.EnableCfeVersion.Value)
            {
                _logger.LogInformation(
                    "CfeVersion not enabled. Skipping {TaskName}",
                    nameof(CfeVersionFeeder));
                
                return;
            }
            
            _logger.LogInformation(
                "Starting {TaskName}",
                nameof(CfeVersionFeeder));

            // Give the consumers some time to connect
            await Task.Delay(_configuration.FeedingDelay.Value, _pipeline.CancellationToken);
            
            // Start a loop fetching CfeVersion Info
            await ProvideCfeVersionInfo(_pipeline.CancellationToken);
            
            _logger.LogInformation(
                "Stopping {TaskName}",
                nameof(CfeVersionFeeder));
        }

        private async Task ProvideCfeVersionInfo(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            var lastVersion = await GetLastCfeVersion(cancellationToken);
            
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var now = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
                if (now == lastVersion)
                {
                    _logger.LogInformation(
                        "No new CFE Version to announce. Last CFE Version Info is still {CfeVersion}",
                        lastVersion);
                    
                    await Task.Delay(_configuration.CfeVersionQueryDelay.Value.RandomizeTime(), cancellationToken);
                    continue;
                }
                
                var cfeVersionInfo = await GetCfeVersion(cancellationToken);
                var lastBlockInfo = await GetLastBlock(cancellationToken);
                
                if (cancellationToken.IsCancellationRequested)
                    return;
                
                if (cfeVersionInfo == null || lastBlockInfo == null)
                {
                    await Task.Delay(_configuration.CfeVersionQueryDelay.Value.RandomizeTime(), cancellationToken);
                    continue;                    
                }
                
                var cfeVersions = cfeVersionInfo
                    .Data.Data.Data
                    .Select(x => new CfeVersionInfo(x.Data, lastBlockInfo.Data.Data.Data[0].Id))
                    .OrderBy(x => x.Version)
                    .ToArray();
                
                _logger.LogInformation(
                    "Broadcasting {TotalVersions} CFE Versions",
                    cfeVersions.Length);

                var allCfeVersions = new CfeVersionsInfo(lastVersion, cfeVersions);
                await _pipeline.Source.SendAsync(
                    allCfeVersions, 
                    cancellationToken);
                
                lastVersion = now;
                await StoreLastCfeVersion(lastVersion);
                
                await Task.Delay(_configuration.CfeVersionQueryDelay.Value.RandomizeTime(), cancellationToken);
            }
        }
        
        private async Task<string> GetLastCfeVersion(CancellationToken cancellationToken)
        {
            if (File.Exists(_configuration.LastCfeVersionLocation))
                return await File.ReadAllTextAsync(_configuration.LastCfeVersionLocation, cancellationToken);

            var yesterday = $"{DateTime.UtcNow.Date.AddDays(-1):yyyy-MM-dd}";
            
            await using var file = File.CreateText(_configuration.LastCfeVersionLocation);
            await file.WriteAsync(yesterday);
            return yesterday;
        }
        
        private async Task StoreLastCfeVersion(string cfeVersion)
        {
            await using var file = File.CreateText(_configuration.LastCfeVersionLocation);
            await file.WriteAsync(cfeVersion.ToString(CultureInfo.InvariantCulture));
        }
        
        private async Task<CfeVersionResponse?> GetCfeVersion(
            CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("Graph");

            var graphQuery = $"{{ \"query\": \"{CfeVersionQuery.ReplaceLineEndings("\\n")}\" }}";
            
            var response = await client.PostAsync(
                string.Empty,
                new StringContent(
                    graphQuery, 
                    new MediaTypeHeaderValue(MediaTypeNames.Application.Json)), 
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<CfeVersionResponse>(cancellationToken: cancellationToken);
        }
        
        private async Task<LastBlockResponse?> GetLastBlock(
            CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("Graph");

            var graphQuery = $"{{ \"query\": \"{LastBlockQuery.ReplaceLineEndings("\\n")}\" }}";
            
            var response = await client.PostAsync(
                string.Empty,
                new StringContent(
                    graphQuery, 
                    new MediaTypeHeaderValue(MediaTypeNames.Application.Json)), 
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<LastBlockResponse>(cancellationToken: cancellationToken);
        }
    }
}