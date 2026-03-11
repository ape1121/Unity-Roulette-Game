using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        private UnityAction _boundAction;

        private void OnDisable()
        {
            ClearAction();
        }

        private void OnValidate()
        {
            actionButton ??= UIReferenceUtility.FindButtonByName(this, "CardAction");
        }

        public void Bind(ResolvedReward reward, Color rarityColor)
        {
            bool hasReward = reward.HasReward;

            ClearAction();

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

        public void SetActionVisible(bool isVisible)
        {
            if (actionButton != null)
                actionButton.gameObject.SetActive(isVisible);
        }

        public void SetActionInteractable(bool isInteractable)
        {
            if (actionButton != null)
                actionButton.interactable = isInteractable;
        }

        public void BindAction(UnityAction onClick, bool isInteractable = true)
        {
            ClearAction();

            if (actionButton == null || onClick == null)
                return;

            _boundAction = onClick;
            actionButton.onClick.AddListener(_boundAction);
            actionButton.gameObject.SetActive(true);
            actionButton.interactable = isInteractable;
        }

        public void ClearAction()
        {
            if (actionButton == null)
                return;

            if (_boundAction != null)
            {
                actionButton.onClick.RemoveListener(_boundAction);
                _boundAction = null;
            }

            actionButton.interactable = false;
            actionButton.gameObject.SetActive(false);
        }
    }
}
