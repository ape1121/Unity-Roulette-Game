using System;
using System.Collections.Generic;
using Ape.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CaseOpenUI : MonoBehaviour
    {
        private enum PresentationState
        {
            Hidden,
            Preview,
            Rolling,
            Result
        }

        private GameUiTextConfig _textConfig;
        private RewardManager _rewardManager;
        private bool _missingReferencesLogged;

        [SerializeField] private RectTransform _viewportRect;
        [SerializeField] private RectTransform _contentRect;
        [SerializeField] private RewardCardUI _rewardCardPrefab;
        [SerializeField] private RectTransform _buttonsRoot;
        [SerializeField] private Button _rollButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TextMeshProUGUI _rollButtonLabel;
        [SerializeField] private TextMeshProUGUI _backButtonLabel;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _costLabel;
        [SerializeField] private TextMeshProUGUI _resultLabel;
        [SerializeField] private Image _centerMarker;

        [Header("Layout")]
        [Min(96f)] [SerializeField] private float _cardWidth = 150f;
        [Min(96f)] [SerializeField] private float _cardHeight = 180f;
        [Min(0f)] [SerializeField] private float _cardSpacing = 18f;
        [Min(3)] [SerializeField] private int _previewCycleCount = 6;

        [Header("Animation")]
        [Min(20f)] [SerializeField] private float _previewScrollSpeed = 140f;
        [Min(0.1f)] [SerializeField] private float _rampUpDuration = 0.45f;
        [Min(0.1f)] [SerializeField] private float _cruiseDuration = 1.15f;
        [Min(0.1f)] [SerializeField] private float _slowDownDuration = 1.9f;
        [Min(0.05f)] [SerializeField] private float _settleDuration = 0.28f;
        [Min(0f)] [SerializeField] private float _spinTravelDistance = 960f;
        [Min(0f)] [SerializeField] private float _overshootDistance = 26f;
        [SerializeField] private Ease _rampUpEase = Ease.InQuad;
        [SerializeField] private Ease _cruiseEase = Ease.Linear;
        [SerializeField] private Ease _slowDownEase = Ease.OutCubic;
        [SerializeField] private Ease _settleEase = Ease.OutBack;
        [Min(1f)] [SerializeField] private float _winnerScale = 1.08f;
        [Min(0.05f)] [SerializeField] private float _winnerPulseDuration = 0.18f;

        private readonly List<RewardCardUI> _spawnedCards = new List<RewardCardUI>();
        private readonly List<ResolvedReward> _previewRewards = new List<ResolvedReward>();

        private Sequence _openSequence;
        private Tween _previewLoopTween;
        private Tween _winnerPulseTween;
        private Action<CaseOpenSession> _onRollRequested;
        private Action _onCloseRequested;
        private CaseOpenSession _currentSession;
        private PresentationState _state;
        private bool _canRoll;
        private int _previewUniqueRewardCount;

        public bool IsAnimating => _state == PresentationState.Rolling;

        public void SetPresentationContext(RewardManager rewardManager, GameUiTextConfig textConfig)
        {
            _rewardManager = rewardManager;
            _textConfig = textConfig;
        }

        public void ShowPreview(
            CaseOpenSession session,
            bool canRoll,
            Action<CaseOpenSession> onRollRequested,
            Action onCloseRequested)
        {
            if (!session.IsValid || !HasRequiredSetup())
                return;

            StopAnimation();

            _currentSession = session;
            _onRollRequested = onRollRequested;
            _onCloseRequested = onCloseRequested;
            _state = PresentationState.Preview;

            BuildPreviewCards(session);
            ApplyPreviewTexts(session);
            ApplyPreviewButtons(canRoll);
            SetMarkerVisible(true);
            StartPreviewLoop();
        }

        public void SetRollInteractable(bool canRoll)
        {
            _canRoll = canRoll;

            if (_rollButton != null && _state == PresentationState.Preview)
                _rollButton.interactable = canRoll;
        }

        public void ShowRollResult(CaseOpenResult caseOpenResult)
        {
            if (!caseOpenResult.IsValid || !HasRequiredSetup())
                return;

            StopAnimation();
            BuildCards(caseOpenResult.ReelRewards);

            _state = PresentationState.Rolling;
            ApplyRollingTexts(caseOpenResult);
            SetButtonsVisible(false);

            Canvas.ForceUpdateCanvases();

            int winningIndex = Mathf.Clamp(caseOpenResult.WinningReelIndex, 0, caseOpenResult.ReelRewards.Count - 1);
            float targetX = ResolveTargetContentPosition(winningIndex);
            float step = _cardWidth + _cardSpacing;
            float travelDistance = Mathf.Max(
                _spinTravelDistance,
                ResolveViewportWidth() * 2.2f,
                (winningIndex + 1) * step * 0.75f);
            float startX = targetX + travelDistance;
            float rampUpX = targetX + (travelDistance * 0.72f);
            float cruiseX = targetX + (travelDistance * 0.24f);
            float overshootX = targetX - Mathf.Max(0f, _overshootDistance);

            SetContentPosition(startX);

            _openSequence = DOTween.Sequence();
            _openSequence.Append(_contentRect.DOAnchorPosX(rampUpX, _rampUpDuration).SetEase(_rampUpEase));
            _openSequence.Append(_contentRect.DOAnchorPosX(cruiseX, _cruiseDuration).SetEase(_cruiseEase));
            _openSequence.Append(_contentRect.DOAnchorPosX(overshootX, _slowDownDuration).SetEase(_slowDownEase));
            _openSequence.Append(_contentRect.DOAnchorPosX(targetX, _settleDuration).SetEase(_settleEase));
            _openSequence.AppendCallback(() => CompleteRoll(caseOpenResult, winningIndex));
            _openSequence.OnKill(() => _openSequence = null);
        }

        public void StopAnimation()
        {
            if (_openSequence != null && _openSequence.IsActive())
                _openSequence.Kill();

            if (_previewLoopTween != null && _previewLoopTween.IsActive())
                _previewLoopTween.Kill();

            if (_winnerPulseTween != null && _winnerPulseTween.IsActive())
                _winnerPulseTween.Kill();

            _openSequence = null;
            _previewLoopTween = null;
            _winnerPulseTween = null;
            ResetCardScales();
        }

        public void HidePresentation()
        {
            StopAnimation();

            _currentSession = default;
            _onRollRequested = null;
            _onCloseRequested = null;
            _previewRewards.Clear();
            _previewUniqueRewardCount = 0;
            _state = PresentationState.Hidden;
            _canRoll = false;

            if (_titleLabel != null)
                _titleLabel.text = string.Empty;

            if (_costLabel != null)
                _costLabel.text = string.Empty;

            if (_resultLabel != null)
                _resultLabel.text = string.Empty;

            if (_buttonsRoot != null)
                _buttonsRoot.gameObject.SetActive(false);

            SetMarkerVisible(false);

            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    _spawnedCards[i].gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            BindButtons();
            HidePresentation();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        private void OnDestroy()
        {
            StopAnimation();
        }

        private bool HasRequiredSetup()
        {
            bool hasSetup =
                _viewportRect != null
                && _contentRect != null
                && _rewardCardPrefab != null
                && _buttonsRoot != null
                && _rollButton != null
                && _backButton != null;

            if (hasSetup)
            {
                _missingReferencesLogged = false;
                return true;
            }

            if (_missingReferencesLogged)
                return false;

            _missingReferencesLogged = true;
            Debug.LogWarning(
                "CaseOpenUI is missing required references. Assign the viewport, reel content, reward card prefab, buttons root, roll button, and back button in the prefab.",
                this);
            return false;
        }

        private void BuildPreviewCards(CaseOpenSession session)
        {
            _previewRewards.Clear();
            session.CaseDefinition.PossibleRewards.GetPreviewRewards(_previewRewards);
            _previewUniqueRewardCount = _previewRewards.Count;

            List<ResolvedReward> repeatedRewards = new List<ResolvedReward>(_previewRewards.Count * Mathf.Max(1, _previewCycleCount));
            int repeatCount = Mathf.Max(3, _previewCycleCount);

            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                for (int i = 0; i < _previewRewards.Count; i++)
                    repeatedRewards.Add(_previewRewards[i]);
            }

            BuildCards(repeatedRewards);
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

                ResolvedReward reward = rewards[i];
                Color rarityColor = reward.HasReward && _rewardManager != null
                    ? _rewardManager.GetRarityColor(reward.Rarity, Color.white)
                    : Color.white;

                card.Bind(reward, rarityColor);
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
                card.gameObject.name = $"CaseRewardCard_{_spawnedCards.Count}";
                card.gameObject.SetActive(false);
                _spawnedCards.Add(card);
            }

            return _spawnedCards[index];
        }

        private void StartPreviewLoop()
        {
            if (_contentRect == null || _previewUniqueRewardCount <= 0)
                return;

            Canvas.ForceUpdateCanvases();

            int startIndex = _previewUniqueRewardCount;
            int endIndex = startIndex + _previewUniqueRewardCount;
            float startX = ResolveTargetContentPosition(startIndex);
            float endX = ResolveTargetContentPosition(endIndex);
            float distance = Mathf.Abs(endX - startX);
            float duration = distance / Mathf.Max(20f, _previewScrollSpeed);

            SetContentPosition(startX);
            _previewLoopTween = _contentRect
                .DOAnchorPosX(endX, duration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .OnKill(() => _previewLoopTween = null);
        }

        private void CompleteRoll(CaseOpenResult caseOpenResult, int winningIndex)
        {
            _state = PresentationState.Result;
            PulseWinner(winningIndex);
            ApplyResultTexts(caseOpenResult);
            ApplyResultButtons();
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

        private void ApplyPreviewTexts(CaseOpenSession session)
        {
            if (_titleLabel != null)
                _titleLabel.text = session.CaseReward != null ? session.CaseReward.RewardName : string.Empty;

            if (_costLabel != null)
                _costLabel.text = _textConfig != null
                    ? _textConfig.FormatCaseOpenCostLabel(session.OpenCost)
                    : session.HasOpenCost
                        ? $"{session.OpenCost.RewardName} x{session.OpenCost.Amount}"
                        : "FREE";

            if (_resultLabel != null)
                _resultLabel.text = string.Empty;
        }

        private void ApplyRollingTexts(CaseOpenResult caseOpenResult)
        {
            if (_titleLabel != null)
                _titleLabel.text = caseOpenResult.CaseReward != null ? caseOpenResult.CaseReward.RewardName : string.Empty;

            if (_costLabel != null)
                _costLabel.text = _textConfig != null
                    ? _textConfig.FormatCaseOpenCostLabel(caseOpenResult.OpenCost)
                    : string.Empty;

            if (_resultLabel != null)
                _resultLabel.text = _textConfig != null
                    ? _textConfig.CaseRollingLabel
                    : "ROLLING...";
        }

        private void ApplyResultTexts(CaseOpenResult caseOpenResult)
        {
            if (_resultLabel != null)
            {
                _resultLabel.text = _textConfig != null
                    ? _textConfig.FormatCaseWinnerLabel(caseOpenResult.GrantedReward)
                    : caseOpenResult.GrantedReward.RewardName;
            }
        }

        private void ApplyPreviewButtons(bool canRoll)
        {
            _state = PresentationState.Preview;
            _canRoll = canRoll;

            if (_rollButtonLabel != null)
                _rollButtonLabel.text = _textConfig != null ? _textConfig.CaseRollButtonLabel : "ROLL";

            if (_backButtonLabel != null)
                _backButtonLabel.text = _textConfig != null ? _textConfig.CaseBackButtonLabel : "BACK";

            if (_rollButton != null)
            {
                _rollButton.gameObject.SetActive(true);
                _rollButton.interactable = canRoll;
            }

            if (_backButton != null)
            {
                _backButton.gameObject.SetActive(true);
                _backButton.interactable = true;
            }

            SetButtonsVisible(true);
        }

        private void ApplyResultButtons()
        {
            if (_rollButton != null)
                _rollButton.gameObject.SetActive(false);

            if (_backButton != null)
            {
                _backButton.gameObject.SetActive(true);
                _backButton.interactable = true;
            }

            if (_backButtonLabel != null)
                _backButtonLabel.text = _textConfig != null ? _textConfig.CaseTakeButtonLabel : "TAKE";

            SetButtonsVisible(true);
        }

        private void SetButtonsVisible(bool isVisible)
        {
            if (_buttonsRoot != null)
                _buttonsRoot.gameObject.SetActive(isVisible);
        }

        private void SetMarkerVisible(bool isVisible)
        {
            if (_centerMarker != null)
                _centerMarker.gameObject.SetActive(isVisible);
        }

        private float ResolveTargetContentPosition(int winningIndex)
        {
            float step = _cardWidth + _cardSpacing;
            float winningCenter = (winningIndex * step) + (_cardWidth * 0.5f);
            return (ResolveViewportWidth() * 0.5f) - winningCenter;
        }

        private float ResolveViewportWidth()
        {
            return _viewportRect != null ? _viewportRect.rect.width : 0f;
        }

        private void SetContentPosition(float x)
        {
            if (_contentRect == null)
                return;

            Vector2 anchoredPosition = _contentRect.anchoredPosition;
            anchoredPosition.x = x;
            anchoredPosition.y = 0f;
            _contentRect.anchoredPosition = anchoredPosition;
        }

        private void HandleRollClicked()
        {
            if (_state != PresentationState.Preview || !_currentSession.IsValid || !_canRoll)
                return;

            if (_rollButton != null)
                _rollButton.interactable = false;

            _onRollRequested?.Invoke(_currentSession);

            if (_state == PresentationState.Preview && _rollButton != null)
                _rollButton.interactable = _canRoll;
        }

        private void HandleBackClicked()
        {
            if (_state == PresentationState.Rolling)
                return;

            _onCloseRequested?.Invoke();
        }

        private void BindButtons()
        {
            if (_rollButton != null)
            {
                _rollButton.onClick.RemoveListener(HandleRollClicked);
                _rollButton.onClick.AddListener(HandleRollClicked);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBackClicked);
                _backButton.onClick.AddListener(HandleBackClicked);
            }
        }
    }
}
