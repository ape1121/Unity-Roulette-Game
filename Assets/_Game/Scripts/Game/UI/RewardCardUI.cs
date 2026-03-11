using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    public sealed class RewardCardUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityBorderImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Button actionButton;

        public Button ActionButton => actionButton;

        public void Bind(ResolvedReward reward, Color rarityColor)
        {
            bool hasReward = reward.HasReward;

            if (iconImage != null)
            {
                iconImage.enabled = hasReward && reward.Icon != null;
                iconImage.sprite = hasReward ? reward.Icon : null;
            }

            if (rarityBorderImage != null)
                rarityBorderImage.color = rarityColor;

            if (nameText != null)
                nameText.text = hasReward ? reward.RewardName : string.Empty;

            if (amountText != null)
                amountText.text = hasReward ? reward.FormatAmountLabel() : string.Empty;
        }
    }
}
