using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RouletteRewardSliceUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _rarityBorderImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private Color _bombBorderColor = new Color32(255, 89, 89, 255);

        public RectTransform RootRect => _rootRect;

        public void Bind(RouletteResolvedSlice slice)
        {
            if (slice.IsBomb)
            {
                if (_iconImage != null)
                {
                    _iconImage.enabled = false;
                    _iconImage.sprite = null;
                }

                if (_rarityBorderImage != null)
                    _rarityBorderImage.color = _bombBorderColor;

                if (_nameText != null)
                    _nameText.text = "Bomb";

                if (_amountText != null)
                    _amountText.text = string.Empty;

                return;
            }

            if (_iconImage != null)
            {
                _iconImage.enabled = slice.Reward.RewardData != null && slice.Reward.RewardData.Icon != null;
                _iconImage.sprite = slice.Reward.RewardData != null ? slice.Reward.RewardData.Icon : null;
            }

            if (_rarityBorderImage != null)
                _rarityBorderImage.color = RewardRarityColorUtility.GetColor(slice.Reward.Rarity);

            if (_nameText != null)
                _nameText.text = slice.DisplayName;

            if (_amountText != null)
                _amountText.text = slice.Reward.FormatAmountLabel();
        }

        private void OnValidate()
        {
            _rootRect ??= GetComponent<RectTransform>();
        }
    }
}
