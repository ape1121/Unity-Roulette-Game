using Ape.Data;
using UnityEngine;

namespace Ape.Game
{
    public static class RewardRarityColorUtility
    {
        private static readonly Color CommonColor = new Color32(143, 155, 179, 255);
        private static readonly Color RareColor = new Color32(62, 169, 255, 255);
        private static readonly Color EpicColor = new Color32(195, 92, 255, 255);
        private static readonly Color LegendaryColor = new Color32(255, 178, 58, 255);

        public static Color GetColor(RewardData.RewardRarity rarity)
        {
            return rarity switch
            {
                RewardData.RewardRarity.Rare => RareColor,
                RewardData.RewardRarity.Epic => EpicColor,
                RewardData.RewardRarity.Legendary => LegendaryColor,
                _ => CommonColor
            };
        }
    }
}
