using System;
using System.Collections.Generic;
using Ape.Data;
using Ape.Sounds;
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

        private static readonly Vector2 InvalidViewportSize = new Vector2(-1f, -1f);

        private readonly InventoryWindowAnimationController _panelAnimationController = new InventoryWindowAnimationController();
        private readonly List<RewardCardUI> _spawnedCards = new List<RewardCardUI>();
        private readonly List<ResolvedReward> _previewRewards = new List<ResolvedReward>();
        private readonly List<ResolvedReward> _activeRewards = new List<ResolvedReward>();

        private GameUiTextConfig _textConfig;
        private RewardManager _rewardManager;
        private RoulettePresentationConfig _roulettePresentationConfig;
        private SoundManager _soundManager;
        private bool _missingReferencesLogged;
        private Sequence _spinSequence;
        private Tween _previewLoopTween;
        private Tween _winnerPulseTween;
        private Action<CaseOpenSession> _onRollRequested;
        private Action _onCloseRequested;
        private CaseOpenSession _currentSession;
        private PresentationState _state;
        private bool _canRoll;
        private int _previewUniqueRewardCount;
        private int _currentWinningIndex = -1;
        private Vector2 _lastViewportSize = InvalidViewportSize;
        private float _resolvedCardWidth;
        private float _resolvedCardHeight;
        private bool _isSlowSpinExcitementPlaying;
#if UNITY_EDITOR
        private bool _editorRefreshQueued;
#endif

        [Header("Structure")]
        [SerializeField] private RectTransform _windowRoot;
        [SerializeField] private CanvasGroup _windowCanvasGroup;
        [SerializeField] private RectTransform _panelRoot;
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
        [Min(32f)] [SerializeField] private float _cardWidth = 150f;
        [Min(32f)] [SerializeField] private float _cardHeight = 180f;
        [Min(0f)] [SerializeField] private float _cardSpacing = 18f;
        [Min(3)] [SerializeField] private int _previewCycleCount = 6;
        [Min(0f)] [SerializeField] private float _centerMarkerWidthPadding = 24f;
        [Min(0f)] [SerializeField] private float _centerMarkerHeightPadding = 24f;

        [Header("Panel Animation")]
        [Min(0.05f)] [SerializeField] private float _fadeDuration = 0.18f;
        [Min(0.05f)] [SerializeField] private float _panelDuration = 0.28f;
        [Min(0f)] [SerializeField] private float _hiddenPanelOffset = 48f;
        [Range(0.5f, 1f)] [SerializeField] private float _hiddenPanelScale = 0.96f;
        [SerializeField] private Ease _openEase = Ease.OutCubic;
        [SerializeField] private Ease _closeEase = Ease.InCubic;

        [Header("Reel Animation")]
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

        [Header("Audio")]
        [Min(0f)] [SerializeField] private float _tickCardOffset;
        [Min(0f)] [SerializeField] private float _tickPitchStep = 0.04f;
        [Min(1)] [SerializeField] private int _tickPitchCycle = 3;
        [Min(0.5f)] [SerializeField] private float _slowSpinExcitementTriggerCards = 2.5f;

        public bool IsAnimating => _state == PresentationState.Rolling;

        public void SetPresentationContext(
            RewardManager rewardManager,
            GameUiTextConfig textConfig,
            RoulettePresentationConfig roulettePresentationConfig,
            SoundManager soundManager)
        {
            _rewardManager = rewardManager;
            _textConfig = textConfig;
            _roulettePresentationConfig = roulettePresentationConfig;
            _soundManager = soundManager;
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
            _currentWinningIndex = -1;

            BuildPreviewCards(session);
            ApplyPreviewTexts(session);
            ApplyPreviewButtons(canRoll);
            SetMarkerVisible(true);
            RefreshResponsiveLayout(force: true);
            PlayOpenTransition();
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

            StopMotionTweens();
            EnsurePanelOpenState();

            _currentWinningIndex = Mathf.Clamp(caseOpenResult.WinningReelIndex, 0, caseOpenResult.ReelRewards.Count - 1);

            BuildCards(caseOpenResult.ReelRewards);
            RefreshResponsiveLayout(force: true);

            _state = PresentationState.Rolling;
            ApplyRollingTexts(caseOpenResult);
            SetButtonsVisible(false);
            SetMarkerVisible(true);

            float targetX = ResolveTargetContentPosition(_currentWinningIndex);
            float step = ResolveCardStep();
            float travelDistance = Mathf.Max(
                _spinTravelDistance,
                ResolveViewportWidth() * 2.2f,
                (_currentWinningIndex + 1) * step * 0.75f);
            float startX = targetX + travelDistance;
            float rampUpX = targetX + (travelDistance * 0.72f);
            float cruiseX = targetX + (travelDistance * 0.24f);
            float overshootX = targetX - Mathf.Max(0f, _overshootDistance);
            int lastTickStep = CalculateTickStep(startX);
            float currentAnimatedX = startX;
            float slowSpinExcitementTriggerDistance = Mathf.Max(ResolveCardStep() * _slowSpinExcitementTriggerCards, ResolveCardStep() * 0.5f);

            SetContentPosition(startX);
            StopSlowSpinExcitement();
            PlayUISound(_roulettePresentationConfig != null ? _roulettePresentationConfig.SpinStartSound : null);

            Action<float> onSpinUpdate = x =>
            {
                currentAnimatedX = x;
                SetContentPosition(x);
                EmitCardTicks(ref lastTickStep, x);
                TryPlaySlowSpinExcitement(targetX, x, slowSpinExcitementTriggerDistance);
            };

            _spinSequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() =>
                {
                    StopSlowSpinExcitement();
                    _spinSequence = null;
                });
            _spinSequence.Append(DOTween.To(() => currentAnimatedX, value => onSpinUpdate(value), rampUpX, _rampUpDuration).SetEase(_rampUpEase));
            _spinSequence.Append(DOTween.To(() => currentAnimatedX, value => onSpinUpdate(value), cruiseX, _cruiseDuration).SetEase(_cruiseEase));
            _spinSequence.Append(DOTween.To(() => currentAnimatedX, value => onSpinUpdate(value), overshootX, _slowDownDuration).SetEase(_slowDownEase));
            _spinSequence.Append(DOTween.To(() => currentAnimatedX, value => onSpinUpdate(value), targetX, _settleDuration).SetEase(_settleEase));
            _spinSequence.AppendCallback(() =>
            {
                StopSlowSpinExcitement();
                PlayUISound(_roulettePresentationConfig != null ? _roulettePresentationConfig.SpinStopSound : null);
                CompleteRoll(caseOpenResult, _currentWinningIndex);
            });
        }

        public void StopAnimation()
        {
            StopMotionTweens();
            _panelAnimationController.KillTransition();
        }

        public void HidePresentation()
        {
            StopAnimation();
            ResetPresentationState();
            ResetHiddenLayout();
            CacheAnimationReferences();
            _panelAnimationController.ApplyClosedState();
        }

        private void Awake()
        {
            CacheAnimationReferences();
            BindButtons();
            ResetPresentationState();
        }

        private void OnEnable()
        {
            CacheAnimationReferences();
            BindButtons();

            if (_state == PresentationState.Hidden)
            {
                ResetHiddenLayout();
                _panelAnimationController.ApplyClosedState();
            }
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        private void OnDestroy()
        {
            StopAnimation();
        }

        private void OnRectTransformDimensionsChange()
        {
            CacheAnimationReferences();

            if (!isActiveAndEnabled || _state == PresentationState.Hidden || _viewportRect == null || _contentRect == null)
                return;

            HandleViewportSizeChanged();
        }

        private void CacheAnimationReferences()
        {
            _windowRoot ??= GetComponent<RectTransform>();
            _windowCanvasGroup ??= GetComponent<CanvasGroup>();

            if (_panelRoot == null)
            {
                RectTransform viewportParent = _viewportRect != null ? _viewportRect.parent as RectTransform : null;
                _panelRoot = viewportParent != null ? viewportParent : _windowRoot;
            }

            _panelAnimationController.Configure(
                _windowCanvasGroup,
                _panelRoot,
                _fadeDuration,
                _panelDuration,
                _hiddenPanelOffset,
                _hiddenPanelScale,
                _openEase,
                _closeEase);
            _panelAnimationController.CachePanelOpenPosition();
        }

        private bool HasRequiredSetup()
        {
            CacheAnimationReferences();

            bool hasSetup =
                _windowCanvasGroup != null
                && _panelRoot != null
                && _viewportRect != null
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
                "CaseOpenUI is missing required references. Assign the root canvas group, panel root, viewport, reel content, reward card prefab, buttons root, roll button, and back button in the prefab.",
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

            _activeRewards.Clear();

            for (int i = 0; i < rewards.Count; i++)
            {
                RewardCardUI card = GetOrCreateCard(i);
                if (card == null)
                    continue;

                ResolvedReward reward = rewards[i];
                Color rarityColor = reward.HasReward && _rewardManager != null
                    ? _rewardManager.GetRarityColor(reward.Rarity, Color.white)
                    : Color.white;

                card.Bind(reward, rarityColor);
                DisableCardInteractions(card);
                card.gameObject.SetActive(true);
                _activeRewards.Add(reward);
            }

            for (int i = rewards.Count; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    _spawnedCards[i].gameObject.SetActive(false);
            }
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
            if (_contentRect == null || _previewUniqueRewardCount <= 0 || _state != PresentationState.Preview)
                return;

            if (_previewLoopTween != null && _previewLoopTween.IsActive())
                _previewLoopTween.Kill();

            RefreshResponsiveLayout(force: false);

            int startIndex = _previewUniqueRewardCount;
            int endIndex = startIndex + _previewUniqueRewardCount;
            float startX = ResolveTargetContentPosition(startIndex);
            float endX = ResolveTargetContentPosition(endIndex);
            float distance = Mathf.Abs(endX - startX);
            if (distance <= 0.01f)
                return;

            float duration = distance / Mathf.Max(20f, _previewScrollSpeed);

            SetContentPosition(startX);
            _previewLoopTween = _contentRect
                .DOAnchorPosX(endX, duration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _previewLoopTween = null);
        }

        private void CompleteRoll(CaseOpenResult caseOpenResult, int winningIndex)
        {
            _state = PresentationState.Result;
            _currentWinningIndex = winningIndex;

            RefreshResponsiveLayout(force: true);
            SetContentPosition(ResolveTargetContentPosition(winningIndex));
            PulseWinner(winningIndex);
            ApplyResultTexts(caseOpenResult);
            ApplyResultButtons();
            PlayUISound(_roulettePresentationConfig != null ? _roulettePresentationConfig.SpinRewardSound : null);
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
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _winnerPulseTween = null);
        }

        private void StopMotionTweens()
        {
            if (_spinSequence != null && _spinSequence.IsActive())
                _spinSequence.Kill();

            if (_previewLoopTween != null && _previewLoopTween.IsActive())
                _previewLoopTween.Kill();

            if (_winnerPulseTween != null && _winnerPulseTween.IsActive())
                _winnerPulseTween.Kill();

            _spinSequence = null;
            _previewLoopTween = null;
            _winnerPulseTween = null;
            StopSlowSpinExcitement();
            ResetCardScales();
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

        private void RefreshResponsiveLayout(bool force)
        {
            if (_viewportRect == null || _contentRect == null || _rewardCardPrefab == null)
                return;

            Canvas.ForceUpdateCanvases();

            Vector2 viewportSize = _viewportRect.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
                return;

            if (!force && Approximately(viewportSize, _lastViewportSize))
                return;

            ResolveCardDimensions(viewportSize);
            ApplyCardLayout();
            ApplyCenterMarkerLayout();
            _lastViewportSize = viewportSize;
        }

        private void ResetPresentationState()
        {
            _currentSession = default;
            _onRollRequested = null;
            _onCloseRequested = null;
            _previewRewards.Clear();
            _activeRewards.Clear();
            _previewUniqueRewardCount = 0;
            _currentWinningIndex = -1;
            _state = PresentationState.Hidden;
            _canRoll = false;
            _lastViewportSize = InvalidViewportSize;
            StopSlowSpinExcitement();

            if (_titleLabel != null)
                _titleLabel.text = string.Empty;

            if (_costLabel != null)
                _costLabel.text = string.Empty;

            if (_resultLabel != null)
                _resultLabel.text = string.Empty;

            SetButtonsVisible(false);
            SetMarkerVisible(false);
            SetButtonsInteractable(false);

            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    _spawnedCards[i].gameObject.SetActive(false);
            }
        }

        private void ResetHiddenLayout()
        {
            if (_contentRect == null)
                return;

            _contentRect.anchorMin = new Vector2(0f, 0.5f);
            _contentRect.anchorMax = new Vector2(0f, 0.5f);
            _contentRect.pivot = new Vector2(0f, 0.5f);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = Vector2.zero;
        }

        private void ResolveCardDimensions(Vector2 viewportSize)
        {
            float aspectRatio = ResolveCardAspectRatio();
            _resolvedCardHeight = Mathf.Max(0f, viewportSize.y);
            _resolvedCardWidth = _resolvedCardHeight * aspectRatio;
        }

        private float ResolveCardAspectRatio()
        {
            RectTransform prefabRect = _rewardCardPrefab != null ? _rewardCardPrefab.transform as RectTransform : null;
            if (prefabRect != null)
            {
                Vector2 prefabSize = prefabRect.rect.size;
                if (prefabSize.x > 0f && prefabSize.y > 0f)
                    return prefabSize.x / prefabSize.y;

                Vector2 sizeDelta = prefabRect.sizeDelta;
                if (sizeDelta.x > 0f && sizeDelta.y > 0f)
                    return sizeDelta.x / sizeDelta.y;
            }

            return Mathf.Max(0.01f, _cardWidth) / Mathf.Max(0.01f, _cardHeight);
        }

        private void ApplyCardLayout()
        {
            float step = ResolveCardStep();

            for (int i = 0; i < _activeRewards.Count; i++)
            {
                RewardCardUI card = _spawnedCards[i];
                if (card == null)
                    continue;

                RectTransform cardRect = card.transform as RectTransform;
                if (cardRect == null)
                    continue;

                cardRect.anchorMin = new Vector2(0f, 0.5f);
                cardRect.anchorMax = new Vector2(0f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.sizeDelta = new Vector2(_resolvedCardWidth, _resolvedCardHeight);
                cardRect.anchoredPosition = new Vector2((_resolvedCardWidth * 0.5f) + (i * step), 0f);
                cardRect.localScale = Vector3.one;
            }

            _contentRect.anchorMin = new Vector2(0f, 0.5f);
            _contentRect.anchorMax = new Vector2(0f, 0.5f);
            _contentRect.pivot = new Vector2(0f, 0.5f);
            _contentRect.sizeDelta = new Vector2(
                Mathf.Max(0f, (_activeRewards.Count * step) - _cardSpacing),
                _resolvedCardHeight);
        }

        private void ApplyCenterMarkerLayout()
        {
            if (_centerMarker == null)
                return;

            RectTransform markerRect = _centerMarker.rectTransform;
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(
                _resolvedCardWidth + _centerMarkerWidthPadding,
                _resolvedCardHeight + _centerMarkerHeightPadding);
        }

        private void HandleViewportSizeChanged()
        {
            Vector2 viewportSize = _viewportRect.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f || Approximately(viewportSize, _lastViewportSize))
                return;

            RefreshResponsiveLayout(force: true);

            if (_state == PresentationState.Preview)
            {
                StartPreviewLoop();
                return;
            }

            if (_state == PresentationState.Result && _currentWinningIndex >= 0)
                SetContentPosition(ResolveTargetContentPosition(_currentWinningIndex));
        }

        private void PlayOpenTransition()
        {
            CacheAnimationReferences();
            _panelAnimationController.SetInteractionState(true);
            _panelAnimationController.PlayTransition(gameObject, show: true, instant: false, onHidden: null);
        }

        private void EnsurePanelOpenState()
        {
            CacheAnimationReferences();
            _panelAnimationController.KillTransition();
            _panelAnimationController.ApplyOpenState();
            _panelAnimationController.SetInteractionState(true);
        }

        private void PlayCloseTransition(Action onClosed)
        {
            CacheAnimationReferences();
            _panelAnimationController.SetInteractionState(false);
            _panelAnimationController.PlayTransition(gameObject, show: false, instant: false, onClosed);
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

        private void SetButtonsInteractable(bool isInteractable)
        {
            if (_rollButton != null)
                _rollButton.interactable = isInteractable && _canRoll;

            if (_backButton != null)
                _backButton.interactable = isInteractable;
        }

        private void SetMarkerVisible(bool isVisible)
        {
            if (_centerMarker != null)
                _centerMarker.gameObject.SetActive(isVisible);
        }

        private float ResolveCardStep()
        {
            return _resolvedCardWidth + _cardSpacing;
        }

        private float ResolveTargetContentPosition(int winningIndex)
        {
            float winningCenter = (_resolvedCardWidth * 0.5f) + (winningIndex * ResolveCardStep());
            return (ResolveViewportWidth() * 0.5f) - winningCenter;
        }

        private float ResolveViewportWidth()
        {
            return _viewportRect != null ? _viewportRect.rect.width : 0f;
        }

        private int CalculateTickStep(float contentPositionX)
        {
            float cardStep = ResolveCardStep();
            if (cardStep <= 0.01f)
                return 0;

            float viewportCenter = ResolveViewportWidth() * 0.5f;
            float centeredCardIndex = (viewportCenter - contentPositionX - (_resolvedCardWidth * 0.5f)) / cardStep;
            return Mathf.FloorToInt(centeredCardIndex + _tickCardOffset);
        }

        private void EmitCardTicks(ref int lastTickStep, float contentPositionX)
        {
            int currentStep = CalculateTickStep(contentPositionX);
            if (currentStep == lastTickStep || _activeRewards.Count <= 0)
                return;

            int stepDirection = currentStep > lastTickStep ? 1 : -1;
            for (int step = lastTickStep + stepDirection; step != currentStep + stepDirection; step += stepDirection)
                PlayTickSound(stepDirection > 0 ? step : step + 1);

            lastTickStep = currentStep;
        }

        private void PlayTickSound(int step)
        {
            if (_activeRewards.Count <= 0)
                return;

            int pitchCycle = Mathf.Max(1, _tickPitchCycle);
            int rewardIndex = ((step % _activeRewards.Count) + _activeRewards.Count) % _activeRewards.Count;
            float pitchMultiplier = 1f + ((rewardIndex % pitchCycle) * _tickPitchStep);
            PlayUISound(_roulettePresentationConfig != null ? _roulettePresentationConfig.SpinTickSound : null, pitchMultiplier);
        }

        private void TryPlaySlowSpinExcitement(float targetX, float currentX, float triggerDistance)
        {
            Sound slowSpinSound = _roulettePresentationConfig != null ? _roulettePresentationConfig.SpinSlowExcitementSound : null;
            if (_isSlowSpinExcitementPlaying || triggerDistance <= 0f || _soundManager == null || slowSpinSound == null)
                return;

            if (Mathf.Abs(currentX - targetX) > triggerDistance)
                return;

            _isSlowSpinExcitementPlaying = true;
            PlayUISound(slowSpinSound);
        }

        private void StopSlowSpinExcitement()
        {
            if (!_isSlowSpinExcitementPlaying)
                return;

            _isSlowSpinExcitementPlaying = false;

            if (_soundManager != null && _roulettePresentationConfig != null && _roulettePresentationConfig.SpinSlowExcitementSound != null)
                _soundManager.StopSound(_roulettePresentationConfig.SpinSlowExcitementSound);
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

            StopMotionTweens();
            SetButtonsInteractable(false);
            PlayCloseTransition(() => _onCloseRequested?.Invoke());
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

        private static void DisableCardInteractions(RewardCardUI card)
        {
            if (card == null)
                return;

            Selectable[] selectables = card.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
                selectables[i].interactable = false;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }

        private void PlayUISound(Sound sound, float pitchMultiplier = 1f)
        {
            if (_soundManager == null || sound == null)
                return;

            _soundManager.PlaySound(sound, isUI: true, pitchMultiplier: pitchMultiplier);
        }
    }
}
