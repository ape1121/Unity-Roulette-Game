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

        public void Bind(ResolvedReward reward, Color rarityColor)
        {
            bool hasReward = reward.HasReward;

            if (_iconImage != null)
            {
                _iconImage.enabled = hasReward && reward.Icon != null;
                _iconImage.sprite = hasReward ? reward.Icon : null;
            }

            if (_rarityBorderImage != null)
                _rarityBorderImage.color = rarityColor;

            if (_nameText != null)
                _nameText.text = hasReward ? reward.RewardName : string.Empty;

            if (_amountText != null)
                _amountText.text = hasReward ? reward.FormatAmountLabel() : string.Empty;
        }
    }
}
