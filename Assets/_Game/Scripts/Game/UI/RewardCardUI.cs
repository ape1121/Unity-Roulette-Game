using Ape.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    public sealed class RewardCardUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _rarityBorderImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _amountText;

        public void Bind(ResolvedReward reward)
        {
            bool hasReward = reward.RewardData != null;

            if (_iconImage != null)
            {
                _iconImage.enabled = hasReward && reward.RewardData.Icon != null;
                _iconImage.sprite = hasReward ? reward.RewardData.Icon : null;
            }

            if (_rarityBorderImage != null)
                _rarityBorderImage.color = hasReward ? App.Game.Rewards.GetRarityData(reward.Rarity).Color : Color.white;

            if (_nameText != null)
                _nameText.text = hasReward ? reward.RewardName : string.Empty;

            if (_amountText != null)
                _amountText.text = hasReward ? $"x{reward.Amount}" : string.Empty;
        }
    }
}
