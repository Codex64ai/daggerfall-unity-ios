// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/BestiaryTextProvider.cs, copied unchanged for iOS. No [Invoke], no edits.
//
using DaggerfallWorkshop.Utility;

namespace DaggerfallBestiaryProject
{
    internal class BestiaryTextProvider : FallbackTextProvider
    {
        public BestiaryTextProvider(ITextProvider fallback)
            : base(fallback)
        {

        }

        public override string GetCustomEnemyName(int enemyId)
        {
            if(BestiaryMod.Instance != null)
            {
                var customEnemies = BestiaryMod.Instance.CustomEnemies;
                if(customEnemies.TryGetValue(enemyId, out var enemy))
                {
                    if (!string.IsNullOrEmpty(enemy.name))
                    {
                        return enemy.name;
                    }
                }
            }

            return base.GetCustomEnemyName(enemyId);
        }
    }
}
