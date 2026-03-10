using Newtonsoft.Json;

namespace EpinelPS.Utils
{
    public class GachaProbabilityConfig
    {
        private static readonly Lazy<GachaProbabilityConfig> LazyCurrent = new(LoadInternal);

        public GachaRate Normal { get; set; } = GachaRate.CreateDefault();
        public GachaRate EventDaily { get; set; } = GachaRate.CreateDefault();

        public static GachaProbabilityConfig Current => LazyCurrent.Value;

        public static (double r, double sr, double ssr) GetNormalizedRates(GachaRate rate)
        {
            double r = Math.Max(0, rate.R);
            double sr = Math.Max(0, rate.SR);
            double ssr = Math.Max(0, rate.SSR);
            double total = r + sr + ssr;

            if (total <= 0)
            {
                return (10, 40, 50);
            }

            double scale = 100.0 / total;
            return (r * scale, sr * scale, ssr * scale);
        }

        public static double ClampPercent(double value) => Math.Clamp(value, 0, 100);

        private static GachaProbabilityConfig LoadInternal()
        {
            try
            {
                string? path = ResolveConfigPath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return new GachaProbabilityConfig();
                }

                GachaProbabilityConfig? config = JsonConvert.DeserializeObject<GachaProbabilityConfig>(File.ReadAllText(path));
                return config ?? new GachaProbabilityConfig();
            }
            catch
            {
                return new GachaProbabilityConfig();
            }
        }

        private static string? ResolveConfigPath()
        {
            string? processDir = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                {
                    processDir = Path.GetDirectoryName(Environment.ProcessPath);
                }
            }
            catch
            {
                processDir = null;
            }

            string?[] candidates =
            [
                processDir,
                Directory.GetCurrentDirectory(),
                AppDomain.CurrentDomain.BaseDirectory
            ];

            foreach (string? dir in candidates)
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                string path = Path.Combine(dir, "gacha_prob.json");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
    }

    public class GachaRate
    {
        public double R { get; set; } = 10;
        public double SR { get; set; } = 40;
        public double SSR { get; set; } = 50;
        public double PilgrimRate { get; set; } = 4.55;
        public double IncreasedChance { get; set; } = 2.0;
        public double IncreasedChancePilgrim { get; set; } = 1.0;

        public static GachaRate CreateDefault() => new();
    }
}
