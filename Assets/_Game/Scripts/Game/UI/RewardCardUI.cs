using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] private Button _actionButton;
        [Header("Highlight")]
        [SerializeField] private float _highlightScaleMultiplier = 1.06f;
        [SerializeField] private float _highlightGrowDuration = 0.16f;
        [SerializeField] private float _highlightSettleDuration = 0.2f;
        [SerializeField] private Ease _highlightGrowEase = Ease.OutCubic;
        [SerializeField] private Ease _highlightSettleEase = Ease.OutBack;

        private UnityAction _boundAction;
        private Tween _highlightTween;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            StopHighlight(resetScale: true);
        }

        private void OnDestroy()
        {
            StopHighlight(resetScale: false);
            ClearAction();
        }

        private void OnValidate()
        {
            _actionButton ??= UIReferenceUtility.FindButtonByName(this, "CardAction");
        }

        public void Bind(ResolvedReward reward, Color rarityColor)
        {
            bool hasReward = reward.HasReward;

            ClearAction();

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

        public void SetActionVisible(bool isVisible)
        {
            if (_actionButton != null)
                _actionButton.gameObject.SetActive(isVisible);
        }

        public void SetActionInteractable(bool isInteractable)
        {
            if (_actionButton != null)
                _actionButton.interactable = isInteractable;
        }

        public void BindAction(UnityAction onClick, bool isInteractable = true)
        {
            ClearAction();

            if (_actionButton == null || onClick == null)
                return;

            _boundAction = onClick;
            _actionButton.onClick.AddListener(_boundAction);
            _actionButton.gameObject.SetActive(true);
            _actionButton.interactable = isInteractable;
        }

        public void ClearAction()
        {
            if (_actionButton == null)
                return;

            if (_boundAction != null)
            {
                _actionButton.onClick.RemoveListener(_boundAction);
                _boundAction = null;
            }

            _actionButton.interactable = false;
            _actionButton.gameObject.SetActive(false);
        }

        public void PlayHighlightPulse()
        {
            Transform targetTransform = transform;
            StopHighlight(resetScale: true);
            _baseScale = targetTransform.localScale;

            _highlightTween = DOTween.Sequence()
                .Append(targetTransform.DOScale(_baseScale * _highlightScaleMultiplier, _highlightGrowDuration).SetEase(_highlightGrowEase))
                .Append(targetTransform.DOScale(_baseScale, _highlightSettleDuration).SetEase(_highlightSettleEase))
                .OnKill(() =>
                {
                    if (targetTransform != null)
                        targetTransform.localScale = _baseScale;

                    _highlightTween = null;
                });
        }

        private void StopHighlight(bool resetScale)
        {
            if (_highlightTween != null && _highlightTween.IsActive())
                _highlightTween.Kill();

            _highlightTween = null;

            if (resetScale != true)
                return;

            transform.localScale = _baseScale;
        }
    }
}
