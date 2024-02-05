namespace ChainflipInsights
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using ChainflipInsights.Consumers.Discord;
    using ChainflipInsights.Consumers.Mastodon;
    using ChainflipInsights.Consumers.Telegram;
    using ChainflipInsights.Consumers.Twitter;
    using ChainflipInsights.Feeders.CexMovement;
    using ChainflipInsights.Feeders.CfeVersion;
    using ChainflipInsights.Feeders.Epoch;
    using ChainflipInsights.Feeders.Funding;
    using ChainflipInsights.Feeders.Redemption;
    using ChainflipInsights.Feeders.Liquidity;
    using ChainflipInsights.Feeders.Swap;
    using ChainflipInsights.Infrastructure.Pipelines;
    using Microsoft.Extensions.Logging;

    public class Runner
    {
        private readonly ILogger<Runner> _logger;
        private readonly DiscordConsumer _discordConsumer;
        private readonly TelegramConsumer _telegramConsumer;
        private readonly TwitterConsumer _twitterConsumer;
        private readonly MastodonConsumer _mastodonConsumer;

        private ITargetBlock<BroadcastInfo> _discordPipelineTarget = null!;
        private ITargetBlock<BroadcastInfo> _telegramPipelineTarget = null!;
        private ITargetBlock<BroadcastInfo> _twitterPipelineTarget = null!;
        private ITargetBlock<BroadcastInfo> _mastodonPipelineTarget = null!;

        public Runner(
            ILogger<Runner> logger,
            Pipeline<SwapInfo> swapPipeline,
            Pipeline<IncomingLiquidityInfo> incomingLiquidityPipeline,
            Pipeline<EpochInfo> epochPipeline,
            Pipeline<FundingInfo> fundingPipeline,
            Pipeline<RedemptionInfo> redemptionPipeline,
            Pipeline<CexMovementInfo> cexMovementPipeline,
            Pipeline<CfeVersionInfo> cfeVersionPipeline,
            DiscordConsumer discordConsumer,
            TelegramConsumer telegramConsumer,
            TwitterConsumer twitterConsumer,
            MastodonConsumer mastodonConsumer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _discordConsumer = discordConsumer ?? throw new ArgumentNullException(nameof(discordConsumer));
            _telegramConsumer = telegramConsumer ?? throw new ArgumentNullException(nameof(telegramConsumer));
            _twitterConsumer = twitterConsumer ?? throw new ArgumentNullException(nameof(twitterConsumer));
            _mastodonConsumer = mastodonConsumer ?? throw new ArgumentNullException(nameof(mastodonConsumer));

            SetupPipelines(
                swapPipeline,
                incomingLiquidityPipeline,
                epochPipeline,
                fundingPipeline,
                redemptionPipeline,
                cexMovementPipeline,
                cfeVersionPipeline);
        }

        private void SetupPipelines(
            Pipeline<SwapInfo> swapPipeline, 
            Pipeline<IncomingLiquidityInfo> incomingLiquidityPipeline, 
            Pipeline<EpochInfo> epochPipeline, 
            Pipeline<FundingInfo> fundingPipeline, 
            Pipeline<RedemptionInfo> redemptionPipeline, 
            Pipeline<CexMovementInfo> cexMovementPipeline, 
            Pipeline<CfeVersionInfo> cfeVersionPipeline)
        {
            var swapSource = swapPipeline.Source;
            swapSource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Swap Source completed, {Status}",
                    task.Status),
                swapPipeline.CancellationToken);
            
            var incomingLiquiditySource = incomingLiquidityPipeline.Source;
            incomingLiquiditySource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Incoming Liquidity Source completed, {Status}",
                    task.Status),
                incomingLiquidityPipeline.CancellationToken);
            
            var epochSource = epochPipeline.Source;
            epochSource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Epoch Source completed, {Status}",
                    task.Status),
                epochPipeline.CancellationToken);
            
            var fundingSource = fundingPipeline.Source;
            fundingSource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Funding Source completed, {Status}",
                    task.Status),
                fundingPipeline.CancellationToken);
            
            var redemptionSource = redemptionPipeline.Source;
            redemptionSource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Redemption Source completed, {Status}",
                    task.Status),
                redemptionPipeline.CancellationToken);
            
            var cexMovementSource = cexMovementPipeline.Source;
            cexMovementSource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "CEX Movement Source completed, {Status}",
                    task.Status),
                cexMovementPipeline.CancellationToken);
            
            var cfeVersionSource = cfeVersionPipeline.Source;
            cfeVersionSource.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "CFE Version Source completed, {Status}",
                    task.Status),
                cfeVersionPipeline.CancellationToken);
            
            var wrapSwaps = new TransformBlock<SwapInfo, BroadcastInfo>(
                swapInfo => new BroadcastInfo(swapInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = swapPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });

            wrapSwaps.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap Swaps completed, {Status}",
                    task.Status),
                swapPipeline.CancellationToken);
            
            var wrapIncomingLiquidity = new TransformBlock<IncomingLiquidityInfo, BroadcastInfo>(
                incomingLiquidityInfo => new BroadcastInfo(incomingLiquidityInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = incomingLiquidityPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });
            
            wrapIncomingLiquidity.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap Incoming Liquidity completed, {Status}",
                    task.Status),
                incomingLiquidityPipeline.CancellationToken);
            
            var wrapEpoch = new TransformBlock<EpochInfo, BroadcastInfo>(
                epochInfo => new BroadcastInfo(epochInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = epochPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });
            
            wrapEpoch.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap Epoch completed, {Status}",
                    task.Status),
                epochPipeline.CancellationToken);
            
            var wrapFunding = new TransformBlock<FundingInfo, BroadcastInfo>(
                fundingInfo => new BroadcastInfo(fundingInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = fundingPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });
            
            wrapFunding.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap Funding completed, {Status}",
                    task.Status),
                fundingPipeline.CancellationToken);
            
            var wrapRedemption = new TransformBlock<RedemptionInfo, BroadcastInfo>(
                redemptionInfo => new BroadcastInfo(redemptionInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = redemptionPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });
            
            wrapRedemption.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap Redemption completed, {Status}",
                    task.Status),
                redemptionPipeline.CancellationToken);
            
            var wrapCexMovement = new TransformBlock<CexMovementInfo, BroadcastInfo>(
                cexMovementInfo => new BroadcastInfo(cexMovementInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = cexMovementPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });
            
            wrapCexMovement.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap CEX Movement completed, {Status}",
                    task.Status),
                cexMovementPipeline.CancellationToken);
            
            var wrapCfeVersion = new TransformBlock<CfeVersionInfo, BroadcastInfo>(
                cfeVersionInfo => new BroadcastInfo(cfeVersionInfo),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = cfeVersionPipeline.CancellationToken,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
                    SingleProducerConstrained = true
                });
            
            wrapCfeVersion.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Wrap CFE Version completed, {Status}",
                    task.Status),
                cfeVersionPipeline.CancellationToken);
            
            var broadcast = new BroadcastBlock<BroadcastInfo>(
                e => e,
                new DataflowBlockOptions
                {
                    CancellationToken = swapPipeline.CancellationToken
                });
            
            broadcast.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Broadcast completed, {Status}",
                    task.Status),
                swapPipeline.CancellationToken);
            
            var linkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };
            
            _discordPipelineTarget = SetupDiscordPipeline(
                broadcast,
                linkOptions, 
                swapPipeline.CancellationToken);
            
            _telegramPipelineTarget = SetupTelegramPipeline(
                broadcast,
                linkOptions,
                swapPipeline.CancellationToken);
            
            _twitterPipelineTarget = SetupTwitterPipeline(
                broadcast,
                linkOptions,
                swapPipeline.CancellationToken);
            
            _mastodonPipelineTarget = SetupMastodonPipeline(
                broadcast,
                linkOptions,
                swapPipeline.CancellationToken);
            
            swapSource.LinkTo(wrapSwaps, linkOptions);
            incomingLiquiditySource.LinkTo(wrapIncomingLiquidity, linkOptions);
            epochSource.LinkTo(wrapEpoch, linkOptions);
            fundingSource.LinkTo(wrapFunding, linkOptions);
            redemptionSource.LinkTo(wrapRedemption, linkOptions);
            cexMovementSource.LinkTo(wrapCexMovement, linkOptions);
            cfeVersionSource.LinkTo(wrapCfeVersion, linkOptions);

            wrapSwaps.LinkTo(broadcast, linkOptions);
            wrapIncomingLiquidity.LinkTo(broadcast, linkOptions);
            wrapEpoch.LinkTo(broadcast, linkOptions);
            wrapFunding.LinkTo(broadcast, linkOptions);
            wrapRedemption.LinkTo(broadcast, linkOptions);
            wrapCexMovement.LinkTo(broadcast, linkOptions);
            wrapCfeVersion.LinkTo(broadcast, linkOptions);
        }
        
        private ITargetBlock<BroadcastInfo> SetupDiscordPipeline(
            BroadcastBlock<BroadcastInfo> broadcast, 
            DataflowLinkOptions linkOptions,
            CancellationToken cancellationToken)
        {
            var pipeline = _discordConsumer.Build(cancellationToken);

            pipeline.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Discord Pipeline completed, {Status}",
                    task.Status),
                cancellationToken);
            
            broadcast.LinkTo(pipeline, linkOptions);

            return pipeline;
        }

        private ITargetBlock<BroadcastInfo> SetupTelegramPipeline(
            BroadcastBlock<BroadcastInfo> broadcast, 
            DataflowLinkOptions linkOptions,
            CancellationToken cancellationToken)
        {
            var pipeline = _telegramConsumer.Build(cancellationToken);

            pipeline.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Telegram Pipeline completed, {Status}",
                    task.Status),
                cancellationToken);
            
            broadcast.LinkTo(pipeline, linkOptions);

            return pipeline;
        }
        
        private ITargetBlock<BroadcastInfo> SetupTwitterPipeline(
            BroadcastBlock<BroadcastInfo> broadcast, 
            DataflowLinkOptions linkOptions,
            CancellationToken cancellationToken)
        {
            var pipeline = _twitterConsumer.Build(cancellationToken);

            pipeline.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Twitter Pipeline completed, {Status}",
                    task.Status),
                cancellationToken);
            
            broadcast.LinkTo(pipeline, linkOptions);

            return pipeline;
        }
        
        private ITargetBlock<BroadcastInfo> SetupMastodonPipeline(
            BroadcastBlock<BroadcastInfo> broadcast, 
            DataflowLinkOptions linkOptions,
            CancellationToken cancellationToken)
        {
            var pipeline = _mastodonConsumer.Build(cancellationToken);

            pipeline.Completion.ContinueWith(
                task => _logger.LogInformation(
                    "Mastodon Pipeline completed, {Status}",
                    task.Status),
                cancellationToken);
            
            broadcast.LinkTo(pipeline, linkOptions);

            return pipeline;
        }
        
        public IEnumerable<Task> Start()
        {
            _logger.LogInformation(
                "Starting {TaskName}", nameof(Runner));

            try
            {
                return
                [
                    _discordPipelineTarget.Completion,
                    _telegramPipelineTarget.Completion,
                    _twitterPipelineTarget.Completion,
                    _mastodonPipelineTarget.Completion
                ];
            }
            catch (AggregateException ex)
            {
                ex.Handle(e =>
                {
                    _logger.LogCritical(
                        "Encountered {ExceptionName}: {Message}",
                        e.GetType().Name,
                        e.Message);
                    return true;
                });

                throw;
            }
        }
    }
}