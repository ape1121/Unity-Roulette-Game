using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    public sealed class GameUIOverlayController
    {
        private enum OverlayRootState
        {
            Hidden,
            Showing,
            Visible,
            Hiding
        }

        private readonly Dictionary<RectTransform, Tween> _overlayTweens = new Dictionary<RectTransform, Tween>();
        private readonly Dictionary<RectTransform, OverlayRootState> _overlayRootStates = new Dictionary<RectTransform, OverlayRootState>();
        private readonly Dictionary<RectTransform, Vector2> _overlayShownAnchoredPositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector2> _companionRootBaseAnchoredPositions = new Dictionary<RectTransform, Vector2>();

        private RectTransform _gameOverRoot;
        private RectTransform _cashOutOverlayRoot;
        private RectTransform _overlayShownPositionSource;
        private RectTransform[] _overlaySlideCompanionRoots;
        private float _slideDuration;
        private Ease _showEase;
        private Ease _hideEase;
        private RectTransform _desiredOverlayRoot;

        public void Configure(
            RectTransform gameOverRoot,
            RectTransform cashOutOverlayRoot,
            RectTransform overlayShownPositionSource,
            RectTransform[] overlaySlideCompanionRoots,
            float slideDuration,
            Ease showEase,
            Ease hideEase)
        {
            _gameOverRoot = gameOverRoot;
            _cashOutOverlayRoot = cashOutOverlayRoot;
            _overlayShownPositionSource = overlayShownPositionSource;
            _overlaySlideCompanionRoots = overlaySlideCompanionRoots;
            _slideDuration = slideDuration;
            _showEase = showEase;
            _hideEase = hideEase;
        }

        public void Initialize()
        {
            InitializeSlidingRoot(_gameOverRoot);
            InitializeSlidingRoot(_cashOutOverlayRoot);
            InitializeCompanionRoots(_overlaySlideCompanionRoots);
        }

        public void KillTweens()
        {
            KillOverlayTween(_gameOverRoot);
            KillOverlayTween(_cashOutOverlayRoot);

            if (_overlaySlideCompanionRoots == null)
                return;

            for (int i = 0; i < _overlaySlideCompanionRoots.Length; i++)
                KillOverlayTween(_overlaySlideCompanionRoots[i]);
        }

        public bool ResolveContinueButtonVisibility(Button continueButton, bool canContinue, RectTransform desiredOverlayRoot, bool instant)
        {
            if (continueButton == null)
                return false;

            if (desiredOverlayRoot == _gameOverRoot)
                return canContinue;

            if (instant || GetOverlayRootState(_gameOverRoot) == OverlayRootState.Hidden)
                return false;

            return continueButton.gameObject.activeSelf;
        }

        public void Update(RectTransform desiredRoot, bool instant)
        {
            _desiredOverlayRoot = desiredRoot;

            if (instant)
            {
                SetOverlayRootVisibleImmediate(_gameOverRoot, desiredRoot == _gameOverRoot);
                SetOverlayRootVisibleImmediate(_cashOutOverlayRoot, desiredRoot == _cashOutOverlayRoot);
                return;
            }

            RectTransform occupyingRoot = GetOccupyingOverlayRoot();

            if (occupyingRoot != null && occupyingRoot != desiredRoot)
            {
                HideOverlayRoot(occupyingRoot);
                return;
            }

            if (desiredRoot == null)
                return;

            ShowOverlayRoot(desiredRoot);
        }

        public void UpdateCompanionRoots(bool shiftUp, RectTransform referenceOverlayRoot, bool instant)
        {
            if (_overlaySlideCompanionRoots == null)
                return;

            Vector2 offset = shiftUp ? GetOverlaySlideOffset(referenceOverlayRoot) : Vector2.zero;

            for (int i = 0; i < _overlaySlideCompanionRoots.Length; i++)
            {
                RectTransform root = _overlaySlideCompanionRoots[i];

                if (root == null)
                    continue;

                if (!_companionRootBaseAnchoredPositions.TryGetValue(root, out Vector2 baseAnchoredPosition))
                {
                    baseAnchoredPosition = root.anchoredPosition;
                    _companionRootBaseAnchoredPositions[root] = baseAnchoredPosition;
                }

                Vector2 targetAnchoredPosition = baseAnchoredPosition + offset;

                KillOverlayTween(root);

                if (instant)
                {
                    root.anchoredPosition = targetAnchoredPosition;
                    continue;
                }

                if (root.anchoredPosition == targetAnchoredPosition)
                    continue;

                Tween companionTween = root.DOAnchorPos(targetAnchoredPosition, _slideDuration)
                    .SetEase(shiftUp ? _showEase : _hideEase)
                    .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnKill(() => _overlayTweens.Remove(root));
                _overlayTweens[root] = companionTween;
            }
        }

        private void InitializeSlidingRoot(RectTransform root)
        {
            if (root == null)
                return;

            CacheShownOverlayAnchoredPosition(root);
            _overlayRootStates[root] = OverlayRootState.Hidden;
            root.anchoredPosition = GetHiddenOverlayAnchoredPosition(root);
            root.gameObject.SetActive(false);
        }

        private void InitializeCompanionRoots(RectTransform[] roots)
        {
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
            {
                RectTransform root = roots[i];

                if (root == _gameOverRoot || root == _cashOutOverlayRoot)
                    continue;

                if (root == null)
                    continue;

                _companionRootBaseAnchoredPositions[root] = root.anchoredPosition;
            }
        }

        private RectTransform GetOccupyingOverlayRoot()
        {
            if (IsOverlayRootOccupyingSlot(_gameOverRoot))
                return _gameOverRoot;

            if (IsOverlayRootOccupyingSlot(_cashOutOverlayRoot))
                return _cashOutOverlayRoot;

            return null;
        }

        private bool IsOverlayRootOccupyingSlot(RectTransform root)
        {
            return root != null && GetOverlayRootState(root) != OverlayRootState.Hidden;
        }

        private OverlayRootState GetOverlayRootState(RectTransform root)
        {
            return root != null && _overlayRootStates.TryGetValue(root, out OverlayRootState state)
                ? state
                : OverlayRootState.Hidden;
        }

        private void SetOverlayRootState(RectTransform root, OverlayRootState state)
        {
            if (root != null)
                _overlayRootStates[root] = state;
        }

        private void SetOverlayRootVisibleImmediate(RectTransform root, bool isVisible)
        {
            if (root == null)
                return;

            KillOverlayTween(root);

            if (isVisible)
            {
                root.gameObject.SetActive(true);
                root.anchoredPosition = GetShownOverlayAnchoredPosition(root);
                SetOverlayRootState(root, OverlayRootState.Visible);
                return;
            }

            root.anchoredPosition = GetHiddenOverlayAnchoredPosition(root);
            root.gameObject.SetActive(false);
            SetOverlayRootState(root, OverlayRootState.Hidden);
        }

        private void ShowOverlayRoot(RectTransform root)
        {
            if (root == null)
                return;

            OverlayRootState state = GetOverlayRootState(root);

            if (state == OverlayRootState.Visible || state == OverlayRootState.Showing)
                return;

            KillOverlayTween(root);
            root.gameObject.SetActive(true);

            if (state == OverlayRootState.Hidden)
                root.anchoredPosition = GetHiddenOverlayAnchoredPosition(root);

            SetOverlayRootState(root, OverlayRootState.Showing);
            Tween showTween = root.DOAnchorPos(GetShownOverlayAnchoredPosition(root), _slideDuration)
                .SetEase(_showEase)
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => SetOverlayRootState(root, OverlayRootState.Visible))
                .OnKill(() => _overlayTweens.Remove(root));
            _overlayTweens[root] = showTween;
        }

        private void HideOverlayRoot(RectTransform root)
        {
            if (root == null)
                return;

            OverlayRootState state = GetOverlayRootState(root);

            if (state == OverlayRootState.Hidden || state == OverlayRootState.Hiding)
                return;

            KillOverlayTween(root);
            SetOverlayRootState(root, OverlayRootState.Hiding);

            Tween hideTween = root.DOAnchorPos(GetHiddenOverlayAnchoredPosition(root), _slideDuration)
                .SetEase(_hideEase)
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    root.gameObject.SetActive(false);
                    SetOverlayRootState(root, OverlayRootState.Hidden);

                    if (_desiredOverlayRoot != null && _desiredOverlayRoot != root && GetOccupyingOverlayRoot() == null)
                        ShowOverlayRoot(_desiredOverlayRoot);
                })
                .OnKill(() => _overlayTweens.Remove(root));
            _overlayTweens[root] = hideTween;
        }

        private Vector2 GetOverlaySlideOffset(RectTransform root)
        {
            return root == null
                ? Vector2.zero
                : GetShownOverlayAnchoredPosition(root) - GetHiddenOverlayAnchoredPosition(root);
        }

        private void KillOverlayTween(RectTransform root)
        {
            if (root == null)
                return;

            if (_overlayTweens.TryGetValue(root, out Tween overlayTween) && overlayTween != null && overlayTween.IsActive())
                overlayTween.Kill();

            _overlayTweens.Remove(root);
        }

        private void CacheShownOverlayAnchoredPosition(RectTransform root)
        {
            if (root == null)
                return;

            _overlayShownAnchoredPositions[root] = ResolveShownOverlayAnchoredPosition(root);
        }

        private Vector2 GetShownOverlayAnchoredPosition(RectTransform root)
        {
            if (root == null)
                return Vector2.zero;

            if (_overlayShownPositionSource != null)
                return _overlayShownPositionSource.anchoredPosition;

            if (_overlayShownAnchoredPositions.TryGetValue(root, out Vector2 shownAnchoredPosition))
                return shownAnchoredPosition;

            shownAnchoredPosition = root.anchoredPosition;
            _overlayShownAnchoredPositions[root] = shownAnchoredPosition;
            return shownAnchoredPosition;
        }

        private Vector2 ResolveShownOverlayAnchoredPosition(RectTransform root)
        {
            return _overlayShownPositionSource != null
                ? _overlayShownPositionSource.anchoredPosition
                : root.anchoredPosition;
        }

        private Vector2 GetHiddenOverlayAnchoredPosition(RectTransform root)
        {
            return GetShownOverlayAnchoredPosition(root) + Vector2.down * root.rect.height;
        }
    }
}
