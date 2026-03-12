using System;
using DG.Tweening;
using UnityEngine;

namespace Ape.Game
{
    public sealed class InventoryWindowAnimationController
    {
        private RectTransform _panelRoot;
        private CanvasGroup _windowCanvasGroup;
        private float _fadeDuration;
        private float _panelDuration;
        private float _hiddenPanelOffset;
        private float _hiddenPanelScale;
        private Ease _openEase;
        private Ease _closeEase;
        private bool _hasCachedPanelPosition;
        private Vector2 _panelOpenAnchoredPosition;
        private Sequence _transitionSequence;

        public void Configure(
            CanvasGroup windowCanvasGroup,
            RectTransform panelRoot,
            float fadeDuration,
            float panelDuration,
            float hiddenPanelOffset,
            float hiddenPanelScale,
            Ease openEase,
            Ease closeEase)
        {
            _windowCanvasGroup = windowCanvasGroup;
            _panelRoot = panelRoot;
            _fadeDuration = fadeDuration;
            _panelDuration = panelDuration;
            _hiddenPanelOffset = hiddenPanelOffset;
            _hiddenPanelScale = hiddenPanelScale;
            _openEase = openEase;
            _closeEase = closeEase;
        }

        public void CachePanelOpenPosition()
        {
            if (_panelRoot == null || _hasCachedPanelPosition)
                return;

            _panelOpenAnchoredPosition = _panelRoot.anchoredPosition;
            _hasCachedPanelPosition = true;
        }

        public void SetInteractionState(bool isInteractive)
        {
            if (_windowCanvasGroup == null)
                return;

            _windowCanvasGroup.interactable = isInteractive;
            _windowCanvasGroup.blocksRaycasts = isInteractive;
        }

        public void ApplyOpenState()
        {
            if (_windowCanvasGroup != null)
                _windowCanvasGroup.alpha = 1f;

            if (_panelRoot != null)
            {
                _panelRoot.anchoredPosition = _panelOpenAnchoredPosition;
                _panelRoot.localScale = Vector3.one;
            }
        }

        public void ApplyClosedState()
        {
            ApplyClosedVisualState();
            SetInteractionState(false);
        }

        public void KillTransition()
        {
            if (_transitionSequence != null && _transitionSequence.IsActive())
                _transitionSequence.Kill();

            _transitionSequence = null;
        }

        public void PlayTransition(GameObject owner, bool show, bool instant, Action onHidden)
        {
            if (_windowCanvasGroup == null || _panelRoot == null)
            {
                if (!show)
                    onHidden?.Invoke();

                return;
            }

            KillTransition();

            if (instant)
            {
                if (show)
                {
                    ApplyOpenState();
                }
                else
                {
                    ApplyClosedState();
                    onHidden?.Invoke();
                }

                return;
            }

            if (show)
            {
                ApplyClosedVisualState();
                _transitionSequence = DOTween.Sequence()
                    .SetLink(owner, LinkBehaviour.KillOnDestroy)
                    .OnKill(() => _transitionSequence = null);
                _transitionSequence.Join(_windowCanvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutCubic));
                _transitionSequence.Join(_panelRoot.DOAnchorPos(_panelOpenAnchoredPosition, _panelDuration).SetEase(_openEase));
                _transitionSequence.Join(_panelRoot.DOScale(1f, _panelDuration).SetEase(_openEase));
                return;
            }

            _transitionSequence = DOTween.Sequence()
                .SetLink(owner, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    ApplyClosedState();
                    onHidden?.Invoke();
                })
                .OnKill(() => _transitionSequence = null);
            _transitionSequence.Join(_windowCanvasGroup.DOFade(0f, _fadeDuration).SetEase(Ease.InCubic));
            _transitionSequence.Join(_panelRoot.DOAnchorPos(_panelOpenAnchoredPosition + Vector2.down * _hiddenPanelOffset, _panelDuration).SetEase(_closeEase));
            _transitionSequence.Join(_panelRoot.DOScale(_hiddenPanelScale, _panelDuration).SetEase(_closeEase));
        }

        private void ApplyClosedVisualState()
        {
            if (_windowCanvasGroup != null)
                _windowCanvasGroup.alpha = 0f;

            if (_panelRoot != null)
            {
                _panelRoot.anchoredPosition = _panelOpenAnchoredPosition + Vector2.down * _hiddenPanelOffset;
                _panelRoot.localScale = new Vector3(_hiddenPanelScale, _hiddenPanelScale, 1f);
            }
        }
    }
}
