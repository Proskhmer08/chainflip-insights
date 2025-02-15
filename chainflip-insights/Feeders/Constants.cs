namespace ChainflipInsights.Feeders
{
    using System.Collections.Generic;

    public static class Constants
    {
        public const string Sub1K = "🦐";
        public const string Sub2_5K = "🐟";
        public const string Sub5K = "🦀";
        public const string Sub10K = "🦈";
        public const string Whale = "🐳";
        
        public const string DollarString = "0.00";

        public const string BTC = "btc";
        public const string DOT = "dot";
        public const string ETH = "eth";
        public const string FLIP = "flip";
        public const string USDC = "usdc";
        
        public static readonly Dictionary<string, AssetInfo> SupportedAssets = new()
        {
            {
                BTC,
                new AssetInfo(
                    BTC,
                    "BTC",
                    "Bitcoin",
                    "Bitcoin",
                    8)
            },
            {
                DOT,
                new AssetInfo(
                    DOT,
                    "DOT",
                    "Polkadot",
                    "Polkadot",
                    10)
            },
            {
                ETH,
                new AssetInfo(
                    ETH,
                    "ETH",
                    "Ethereum",
                    "Ethereum",
                    18)
            },
            {
                FLIP,
                new AssetInfo(
                    FLIP,
                    "FLIP",
                    "Chainflip",
                    "Ethereum",
                    18)
            },
            {
                USDC,
                new AssetInfo(
                    USDC,
                    "USDC",
                    "ethUSDC",
                    "Ethereum",
                    6)
            },
        };
    }

    public class AssetInfo
    {
        public string Id { get; }

        public string Ticker { get; }
        
        public int Decimals { get; }
        
        public string FormatString { get; }

        public string Name { get; }

        public  string Network { get; }
        
        public AssetInfo(
            string id,
            string ticker, 
            string name,
            string network, 
            int decimals)
        {
            Id = id;
            Ticker = ticker;
            Name = name;
            Network = network;
            Decimals = decimals;
            
            FormatString = $"0.00{new string('#', decimals - 2)}";
        }
    }
}