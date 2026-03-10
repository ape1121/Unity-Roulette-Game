using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CaseOpenUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _viewportRect;
        [SerializeField] private RectTransform _contentRect;
        [SerializeField] private RewardCardUI _rewardCardPrefab;
        [SerializeField] private TextMeshProUGUI _winnerLabel;

        [Header("Layout")]
        [Min(64f)] [SerializeField] private float _cardWidth = 150f;
        [Min(64f)] [SerializeField] private float _cardHeight = 180f;
        [Min(0f)] [SerializeField] private float _cardSpacing = 18f;
        [Min(0f)] [SerializeField] private float _leadInTravelDistance = 520f;

        [Header("Animation")]
        [Min(0.25f)] [SerializeField] private float _spinDuration = 4.2f;
        [Min(0.05f)] [SerializeField] private float _settleDuration = 0.28f;
        [Min(0f)] [SerializeField] private float _overshootDistance = 28f;
        [SerializeField] private Ease _spinEase = Ease.OutQuint;
        [SerializeField] private Ease _settleEase = Ease.OutBack;
        [Min(1f)] [SerializeField] private float _winnerScale = 1.08f;
        [Min(0.05f)] [SerializeField] private float _winnerPulseDuration = 0.18f;

        private readonly List<RewardCardUI> _spawnedCards = new List<RewardCardUI>();

        private Sequence _openSequence;
        private Tween _winnerPulseTween;

        public bool IsAnimating => _openSequence != null && _openSequence.IsActive();

        public void Play(CaseOpenResult caseOpenResult, System.Action<CaseOpenResult> onCompleted = null)
        {
            if (!caseOpenResult.IsValid || _contentRect == null || _rewardCardPrefab == null)
            {
                onCompleted?.Invoke(caseOpenResult);
                return;
            }

            StopAnimation();
            BuildCards(caseOpenResult.ReelRewards);
            UpdateWinnerLabel(caseOpenResult.GrantedReward);

            int winningIndex = Mathf.Clamp(caseOpenResult.WinningReelIndex, 0, _spawnedCards.Count - 1);
            float targetX = ResolveTargetContentPosition(winningIndex);
            float travelDistance = Mathf.Max(_leadInTravelDistance, ResolveViewportWidth() * 1.2f);
            float startX = targetX - travelDistance;
            float overshootX = targetX + Mathf.Max(0f, _overshootDistance);

            SetContentPosition(startX);

            _openSequence = DOTween.Sequence();
            _openSequence.Append(_contentRect.DOAnchorPosX(overshootX, _spinDuration).SetEase(_spinEase));
            _openSequence.Append(_contentRect.DOAnchorPosX(targetX, _settleDuration).SetEase(_settleEase));
            _openSequence.AppendCallback(() => PulseWinner(winningIndex));
            _openSequence.OnComplete(() => onCompleted?.Invoke(caseOpenResult));
            _openSequence.OnKill(() => _openSequence = null);
        }

        public void StopAnimation()
        {
            if (_openSequence != null && _openSequence.IsActive())
                _openSequence.Kill();

            if (_winnerPulseTween != null && _winnerPulseTween.IsActive())
                _winnerPulseTween.Kill();

            _openSequence = null;
            _winnerPulseTween = null;
            ResetCardScales();
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        private void OnDestroy()
        {
            StopAnimation();
        }

        private void OnValidate()
        {
            _viewportRect ??= GetComponent<RectTransform>();

            if (_contentRect == null && transform.childCount > 0)
                _contentRect = transform.GetChild(0) as RectTransform;
        }

        private void BuildCards(IReadOnlyList<ResolvedReward> rewards)
        {
            if (_contentRect == null)
                return;

            float step = _cardWidth + _cardSpacing;

            for (int i = 0; i < rewards.Count; i++)
            {
                RewardCardUI card = GetOrCreateCard(i);
                if (card == null)
                    continue;

                RectTransform cardRect = card.transform as RectTransform;
                if (cardRect != null)
                {
                    cardRect.anchorMin = new Vector2(0f, 0.5f);
                    cardRect.anchorMax = new Vector2(0f, 0.5f);
                    cardRect.pivot = new Vector2(0f, 0.5f);
                    cardRect.sizeDelta = new Vector2(_cardWidth, _cardHeight);
                    cardRect.anchoredPosition = new Vector2(i * step, 0f);
                    cardRect.localScale = Vector3.one;
                }

                card.Bind(rewards[i]);
                card.gameObject.SetActive(true);
            }

            for (int i = rewards.Count; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    _spawnedCards[i].gameObject.SetActive(false);
            }

            _contentRect.anchorMin = new Vector2(0f, 0.5f);
            _contentRect.anchorMax = new Vector2(0f, 0.5f);
            _contentRect.pivot = new Vector2(0f, 0.5f);
            _contentRect.sizeDelta = new Vector2(Mathf.Max(0f, (rewards.Count * step) - _cardSpacing), _cardHeight);
        }

        private RewardCardUI GetOrCreateCard(int index)
        {
            while (_spawnedCards.Count <= index)
            {
                RewardCardUI card = Instantiate(_rewardCardPrefab, _contentRect);
                card.gameObject.SetActive(false);
                _spawnedCards.Add(card);
            }

            return _spawnedCards[index];
        }

        private void PulseWinner(int winningIndex)
        {
            if (winningIndex < 0 || winningIndex >= _spawnedCards.Count || _spawnedCards[winningIndex] == null)
                return;

            RectTransform winnerRect = _spawnedCards[winningIndex].transform as RectTransform;
            if (winnerRect == null)
                return;

            _winnerPulseTween = winnerRect
                .DOScale(_winnerScale, _winnerPulseDuration)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.OutQuad)
                .OnKill(() => _winnerPulseTween = null);
        }

        private void ResetCardScales()
        {
            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] == null)
                    continue;

                RectTransform cardRect = _spawnedCards[i].transform as RectTransform;
                if (cardRect != null)
                    cardRect.localScale = Vector3.one;
            }
        }

        private float ResolveTargetContentPosition(int winningIndex)
        {
            float step = _cardWidth + _cardSpacing;
            float winningCenter = (winningIndex * step) + (_cardWidth * 0.5f);
            return (ResolveViewportWidth() * 0.5f) - winningCenter;
        }

        private float ResolveViewportWidth()
        {
            if (_viewportRect == null)
                return 0f;

            return _viewportRect.rect.width;
        }

        private void SetContentPosition(float x)
        {
            Vector2 anchoredPosition = _contentRect.anchoredPosition;
            anchoredPosition.x = x;
            anchoredPosition.y = 0f;
            _contentRect.anchoredPosition = anchoredPosition;
        }

        private void UpdateWinnerLabel(ResolvedReward reward)
        {
            if (_winnerLabel == null)
                return;

            _winnerLabel.text = reward.RewardData == null
                ? string.Empty
                : $"{reward.RewardName} {reward.FormatAmountLabel()}";
        }
    }
}
