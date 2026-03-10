using Ape.Core;
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
        [SerializeField] private Sprite _bombIcon;

        public RectTransform RootRect => _rootRect;

        public void Bind(RouletteResolvedSlice slice)
        {
            if (slice.IsBomb)
            {
                if (_iconImage != null)
                {
                    _iconImage.enabled = true;
                    _iconImage.sprite = _bombIcon;
                }

                if (_rarityBorderImage != null)
                    _rarityBorderImage.color = _bombBorderColor;

                if (_nameText != null)
                    _nameText.gameObject.SetActive(false);

                if (_amountText != null)
                    _amountText.text = string.Empty;

                return;
            }

            Color rarityColor = App.Game != null
                ? App.Game.Rewards.GetRarityColor(slice.Reward.Rarity, Color.white)
                : Color.white;

            if (_iconImage != null)
            {
                _iconImage.enabled = slice.Reward.HasReward && slice.Reward.Icon != null;
                _iconImage.sprite = slice.Reward.HasReward ? slice.Reward.Icon : null;
            }

            if (_rarityBorderImage != null)
                _rarityBorderImage.color = rarityColor;

            if (_nameText != null)
            {
                _nameText.gameObject.SetActive(true);
                _nameText.text = slice.DisplayName;
            }

            if (_amountText != null)
                _amountText.text = slice.Reward.FormatAmountLabel();
        }

        private void OnValidate()
        {
            _rootRect ??= GetComponent<RectTransform>();
        }
    }
}
