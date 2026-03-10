using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using Ape.Profile;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace Ape.Game
{
    [MovedFrom(false, sourceNamespace: "Ape.Game", sourceClassName: "RewardInventoryListUI")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryUIWindow : MonoBehaviour
    {
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.76f);
        private static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        private static readonly Color ScrollViewportColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color EmptyStateColor = new Color(1f, 1f, 1f, 0.62f);

        [Header("Structure")]
        [SerializeField] private RectTransform _windowRoot;
        [SerializeField] private CanvasGroup _windowCanvasGroup;
        [SerializeField] private Button _backdropButton;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _viewportRoot;
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private RewardCardUI _rewardCardPrefab;
        [SerializeField] private GameObject _emptyStateRoot;
        [SerializeField] private TextMeshProUGUI _emptyStateText;

        [Header("Animation")]
        [SerializeField] private float _fadeDuration = 0.18f;
        [SerializeField] private float _panelDuration = 0.28f;
        [SerializeField] private float _hiddenPanelOffset = 48f;
        [SerializeField] private float _hiddenPanelScale = 0.96f;
        [SerializeField] private Ease _openEase = Ease.OutCubic;
        [SerializeField] private Ease _closeEase = Ease.InCubic;

        private readonly List<RewardCardUI> _spawnedCards = new List<RewardCardUI>();
        private readonly List<ResolvedReward> _resolvedRewards = new List<ResolvedReward>();

        private Sequence _transitionSequence;
        private bool _isSubscribed;
        private bool _isOpen;
        private bool _hasCachedPanelPosition;
        private Vector2 _panelOpenAnchoredPosition;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            EnsureWindowHierarchy();
            ApplyClosedState(force: true);
        }

        private void OnEnable()
        {
            EnsureWindowHierarchy();
            SubscribeToProfile();
            Refresh();
            ApplyClosedState(force: true);
        }

        private void OnDisable()
        {
            KillTransition();
            UnsubscribeFromProfile();
        }

        private void OnValidate()
        {
            _windowRoot ??= GetComponent<RectTransform>();
            _windowCanvasGroup ??= GetComponent<CanvasGroup>();
        }

        public void Refresh()
        {
            EnsureWindowHierarchy();
            SubscribeToProfile();
            BuildResolvedRewards();
            SyncCardViews();
        }

        public void Open(bool instant = false)
        {
            EnsureWindowHierarchy();
            Refresh();

            _isOpen = true;
            SetInteractionState(true);
            PlayTransition(show: true, instant);
        }

        public void Close(bool instant = false)
        {
            EnsureWindowHierarchy();

            if (!_isOpen && !instant)
                return;

            _isOpen = false;
            SetInteractionState(false);
            PlayTransition(show: false, instant);
        }

        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void SubscribeToProfile()
        {
            if (_isSubscribed || App.Profile == null)
                return;

            App.Profile.DataChanged += HandleProfileDataChanged;
            _isSubscribed = true;
        }

        private void UnsubscribeFromProfile()
        {
            if (!_isSubscribed || App.Profile == null)
                return;

            App.Profile.DataChanged -= HandleProfileDataChanged;
            _isSubscribed = false;
        }

        private void HandleProfileDataChanged(SaveData _)
        {
            Refresh();
        }

        private void HandleBackdropClicked()
        {
            Close();
        }

        private void HandleCloseClicked()
        {
            Close();
        }

        private void EnsureWindowHierarchy()
        {
            _windowRoot ??= GetComponent<RectTransform>();
            if (_windowRoot == null)
                return;

            StretchToParent(_windowRoot);

            _windowCanvasGroup = GetOrAddComponent<CanvasGroup>(_windowRoot.gameObject);
            _backdropButton = EnsureBackdropButton();
            _panelRoot = EnsurePanelRoot();
            _titleText = EnsureTitleText();
            _closeButton = EnsureCloseButton();
            _scrollRect = EnsureScrollRect();
            _viewportRoot = _scrollRect != null ? _scrollRect.viewport : _viewportRoot;
            _contentRoot = _scrollRect != null ? _scrollRect.content : _contentRoot;
            _emptyStateText = EnsureEmptyStateText();
            _emptyStateRoot = _emptyStateText != null ? _emptyStateText.gameObject : _emptyStateRoot;

            if (_titleText != null)
                _titleText.text = "INVENTORY";

            if (_emptyStateText != null)
                _emptyStateText.text = "No inventory rewards collected yet.";

            BindButtons();
            CachePanelOpenPosition();
        }

        private void BindButtons()
        {
            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(HandleBackdropClicked);
                _backdropButton.onClick.AddListener(HandleBackdropClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
                _closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private Button EnsureBackdropButton()
        {
            Button button = _backdropButton;
            if (button == null)
            {
                Transform existing = _windowRoot.Find("DimBackground");
                if (existing != null)
                    button = existing.GetComponent<Button>();
            }

            if (button == null)
            {
                for (int i = 0; i < _windowRoot.childCount; i++)
                {
                    Transform child = _windowRoot.GetChild(i);
                    if (child == null || child == _panelRoot)
                        continue;

                    button = child.GetComponent<Button>();
                    if (button != null)
                        break;

                    Image existingImage = child.GetComponent<Image>();
                    if (existingImage == null)
                        continue;

                    button = GetOrAddComponent<Button>(child.gameObject);
                    break;
                }
            }

            if (button == null)
            {
                GameObject backdropObject = new GameObject("DimBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
                backdropRect.SetParent(_windowRoot, false);
                StretchToParent(backdropRect);
                button = backdropObject.GetComponent<Button>();
            }

            button.gameObject.name = "DimBackground";

            Image image = GetOrAddComponent<Image>(button.gameObject);
            RewardCardUI.ApplyDefaultGraphic(image, BackdropColor);

            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;

            return button;
        }

        private RectTransform EnsurePanelRoot()
        {
            RectTransform panel = _panelRoot;
            if (panel == null)
            {
                Transform existing = _windowRoot.Find("ContentPanel");
                if (existing != null)
                    panel = existing as RectTransform;
            }

            if (panel == null)
            {
                GameObject panelObject = new GameObject("ContentPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
                panel = panelObject.GetComponent<RectTransform>();
                panel.SetParent(_windowRoot, false);
            }

            panel.anchorMin = new Vector2(0.08f, 0.08f);
            panel.anchorMax = new Vector2(0.92f, 0.92f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.pivot = new Vector2(0.5f, 0.5f);

            Image panelImage = GetOrAddComponent<Image>(panel.gameObject);
            RewardCardUI.ApplyDefaultGraphic(panelImage, PanelColor);

            VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(panel.gameObject);
            layoutGroup.padding = new RectOffset(28, 28, 28, 28);
            layoutGroup.spacing = 20;
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            panel.SetAsLastSibling();
            return panel;
        }

        private TextMeshProUGUI EnsureTitleText()
        {
            Transform header = EnsureHeaderRoot();
            Transform title = header.Find("Title");
            if (title == null)
                title = CreateText("Title", header, 28f, FontStyles.Bold, TextAlignmentOptions.Left).transform;

            LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(title.gameObject);
            layoutElement.flexibleWidth = 1f;

            TextMeshProUGUI text = title.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(text, 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            return text;
        }

        private Button EnsureCloseButton()
        {
            Transform header = EnsureHeaderRoot();
            Transform existing = header.Find("CloseButton");
            Button button = existing != null ? existing.GetComponent<Button>() : null;

            if (button == null)
            {
                GameObject buttonObject = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.SetParent(header, false);
                button = buttonObject.GetComponent<Button>();
            }

            LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(button.gameObject);
            layoutElement.preferredWidth = 56f;
            layoutElement.preferredHeight = 56f;
            layoutElement.flexibleWidth = 0f;

            Image image = GetOrAddComponent<Image>(button.gameObject);
            RewardCardUI.ApplyDefaultGraphic(image, new Color(1f, 1f, 1f, 0.08f));

            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 1.10f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null)
                label = CreateText("Label", button.transform, 26f, FontStyles.Bold, TextAlignmentOptions.Center);

            StretchToParent(label.rectTransform);
            ApplyTextStyle(label, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            label.text = "X";

            return button;
        }

        private ScrollRect EnsureScrollRect()
        {
            Transform existing = _panelRoot.Find("ScrollView");
            ScrollRect scrollRect = existing != null ? existing.GetComponent<ScrollRect>() : _scrollRect;
            if (scrollRect == null)
            {
                GameObject scrollObject = new GameObject("ScrollView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
                scrollRectTransform.SetParent(_panelRoot, false);
                scrollRect = scrollObject.GetComponent<ScrollRect>();
            }

            LayoutElement scrollLayout = GetOrAddComponent<LayoutElement>(scrollRect.gameObject);
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.minHeight = 280f;

            Image scrollImage = GetOrAddComponent<Image>(scrollRect.gameObject);
            RewardCardUI.ApplyDefaultGraphic(scrollImage, ScrollViewportColor);

            RectTransform viewport = scrollRect.viewport;
            if (viewport == null)
            {
                Transform existingViewport = scrollRect.transform.Find("Viewport");
                viewport = existingViewport != null ? existingViewport as RectTransform : null;
            }

            if (viewport == null)
            {
                GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
                viewport = viewportObject.GetComponent<RectTransform>();
                viewport.SetParent(scrollRect.transform, false);
                StretchToParent(viewport);
            }

            Image viewportImage = GetOrAddComponent<Image>(viewport.gameObject);
            RewardCardUI.ApplyDefaultGraphic(viewportImage, new Color(1f, 1f, 1f, 0.01f));

            RectTransform content = scrollRect.content;
            if (content == null)
            {
                Transform existingContent = viewport.Find("Content");
                content = existingContent != null ? existingContent as RectTransform : null;
            }

            if (content == null)
            {
                GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content = contentObject.GetComponent<RectTransform>();
                content.SetParent(viewport, false);
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup contentLayout = GetOrAddComponent<VerticalLayoutGroup>(content.gameObject);
            contentLayout.padding = new RectOffset(20, 20, 20, 20);
            contentLayout.spacing = 14;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter fitter = GetOrAddComponent<ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            return scrollRect;
        }

        private TextMeshProUGUI EnsureEmptyStateText()
        {
            if (_viewportRoot == null)
                return _emptyStateText;

            Transform existing = _viewportRoot.Find("EmptyState");
            TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : _emptyStateText;
            if (text == null)
            {
                text = CreateText("EmptyState", _viewportRoot, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
                StretchToParent(text.rectTransform, 32f);
            }

            ApplyTextStyle(text, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            text.color = EmptyStateColor;
            return text;
        }

        private Transform EnsureHeaderRoot()
        {
            Transform existing = _panelRoot.Find("Header");
            if (existing != null)
                return existing;

            GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            RectTransform headerRect = headerObject.GetComponent<RectTransform>();
            headerRect.SetParent(_panelRoot, false);

            LayoutElement layoutElement = headerObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 60f;
            layoutElement.flexibleHeight = 0f;

            HorizontalLayoutGroup layoutGroup = headerObject.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 12;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            return headerRect;
        }

        private void CachePanelOpenPosition()
        {
            if (_panelRoot == null || _hasCachedPanelPosition)
                return;

            _panelOpenAnchoredPosition = _panelRoot.anchoredPosition;
            _hasCachedPanelPosition = true;
        }

        private void PlayTransition(bool show, bool instant)
        {
            if (_windowCanvasGroup == null || _panelRoot == null)
                return;

            KillTransition();

            if (instant)
            {
                if (show)
                    ApplyOpenState(force: true);
                else
                    ApplyClosedState(force: true);

                return;
            }

            if (show)
            {
                ApplyClosedVisualState();
                _transitionSequence = DOTween.Sequence()
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .OnKill(() => _transitionSequence = null);
                _transitionSequence.Join(_windowCanvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutCubic));
                _transitionSequence.Join(_panelRoot.DOAnchorPos(_panelOpenAnchoredPosition, _panelDuration).SetEase(_openEase));
                _transitionSequence.Join(_panelRoot.DOScale(1f, _panelDuration).SetEase(_openEase));
            }
            else
            {
                _transitionSequence = DOTween.Sequence()
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() => ApplyClosedState(force: true))
                    .OnKill(() => _transitionSequence = null);
                _transitionSequence.Join(_windowCanvasGroup.DOFade(0f, _fadeDuration).SetEase(Ease.InCubic));
                _transitionSequence.Join(_panelRoot.DOAnchorPos(_panelOpenAnchoredPosition + Vector2.down * _hiddenPanelOffset, _panelDuration).SetEase(_closeEase));
                _transitionSequence.Join(_panelRoot.DOScale(_hiddenPanelScale, _panelDuration).SetEase(_closeEase));
            }
        }

        private void ApplyOpenState(bool force)
        {
            if (!force && _isOpen)
                return;

            _windowCanvasGroup.alpha = 1f;
            _panelRoot.anchoredPosition = _panelOpenAnchoredPosition;
            _panelRoot.localScale = Vector3.one;
        }

        private void ApplyClosedState(bool force)
        {
            if (!force && !_isOpen)
                return;

            ApplyClosedVisualState();
            SetInteractionState(false);
        }

        private void ApplyClosedVisualState()
        {
            if (_windowCanvasGroup == null || _panelRoot == null)
                return;

            _windowCanvasGroup.alpha = 0f;
            _panelRoot.anchoredPosition = _panelOpenAnchoredPosition + Vector2.down * _hiddenPanelOffset;
            _panelRoot.localScale = new Vector3(_hiddenPanelScale, _hiddenPanelScale, 1f);
        }

        private void SetInteractionState(bool isInteractive)
        {
            if (_windowCanvasGroup == null)
                return;

            _windowCanvasGroup.interactable = isInteractive;
            _windowCanvasGroup.blocksRaycasts = isInteractive;
        }

        private void KillTransition()
        {
            if (_transitionSequence != null && _transitionSequence.IsActive())
                _transitionSequence.Kill();

            _transitionSequence = null;
        }

        private void BuildResolvedRewards()
        {
            _resolvedRewards.Clear();

            if (_contentRoot == null || App.Profile == null)
                return;

            GameConfig gameConfig = App.Config != null
                ? App.Config.GameConfig
                : null;
            IReadOnlyList<RewardInventoryEntry> inventory = App.Profile.Inventory;

            if (gameConfig == null || inventory == null)
                return;

            for (int i = 0; i < inventory.Count; i++)
            {
                RewardInventoryEntry entry = inventory[i];
                if (entry.Amount <= 0)
                    continue;

                if (!gameConfig.TryGetReward(entry.RewardId, out RewardData rewardData) || rewardData == null)
                {
                    Debug.LogWarning($"Reward inventory entry '{entry.RewardId}' could not be resolved from the reward catalog.", this);
                    continue;
                }

                if (rewardData.Kind == RewardData.RewardKind.Cash || rewardData.Kind == RewardData.RewardKind.Gold)
                    continue;

                _resolvedRewards.Add(new ResolvedReward(rewardData, entry.Amount));
            }

            _resolvedRewards.Sort(CompareRewards);
        }

        private void SyncCardViews()
        {
            bool hasRewards = _resolvedRewards.Count > 0 && _contentRoot != null;

            for (int i = 0; i < _resolvedRewards.Count; i++)
            {
                RewardCardUI card = GetOrCreateCard(i);
                if (card == null)
                    continue;

                card.Bind(_resolvedRewards[i]);
                card.gameObject.SetActive(true);
            }

            for (int i = _resolvedRewards.Count; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    _spawnedCards[i].gameObject.SetActive(false);
            }

            if (_emptyStateRoot != null)
                _emptyStateRoot.SetActive(!hasRewards);
        }

        private RewardCardUI GetOrCreateCard(int index)
        {
            if (_contentRoot == null)
                return null;

            while (_spawnedCards.Count <= index)
            {
                RewardCardUI card = _rewardCardPrefab != null
                    ? Instantiate(_rewardCardPrefab, _contentRoot)
                    : RewardCardUI.CreateDefault(_contentRoot);

                if (card == null)
                    break;

                card.gameObject.SetActive(false);
                _spawnedCards.Add(card);
            }

            return _spawnedCards.Count > index ? _spawnedCards[index] : null;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(text, fontSize, fontStyle, alignment);
            return text;
        }

        private static void ApplyTextStyle(TextMeshProUGUI text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            if (text == null)
                return;

            if (text.font == null)
                text.font = TMP_Settings.defaultFontAsset;

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
        }

        private static void StretchToParent(RectTransform rectTransform, float inset = 0f)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (target == null)
                return null;

            T component = target.GetComponent<T>();
            if (component == null)
                component = target.AddComponent<T>();

            return component;
        }

        private static int CompareRewards(ResolvedReward left, ResolvedReward right)
        {
            int rarityComparison = right.Rarity.CompareTo(left.Rarity);
            if (rarityComparison != 0)
                return rarityComparison;

            int nameComparison = string.Compare(left.RewardName, right.RewardName, System.StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0)
                return nameComparison;

            return right.Amount.CompareTo(left.Amount);
        }
    }
}
