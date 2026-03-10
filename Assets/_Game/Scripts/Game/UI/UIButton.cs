using Ape.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButton : Button
{
    [SerializeField] protected Image[] extraImages;
    [SerializeField] private float pressOffset = 4f;
    [SerializeField, Min(0f)] private float pressDownDuration = 0.04f;
    [SerializeField, Min(0f)] private float pressUpDuration = 0.08f;
    [SerializeField] private Ease pressDownEase = Ease.OutQuad;
    [SerializeField] private Ease pressUpEase = Ease.OutCubic;
    [SerializeField] private string clickSound = "";
    [SerializeField] private bool enableHoverScale;
    [SerializeField] private RectTransform hoverScaleTarget;
    [SerializeField, Min(1f)] private float hoverScaleMultiplier = 1.06f;
    [SerializeField, Min(0f)] private float hoverScaleDuration = 0.12f;
    [SerializeField] private Ease hoverScaleEase = Ease.OutCubic;

    private RectTransform rectTransform;
    private Tween pressTween;
    private Tween hoverScaleTween;
    private Vector2 activePressBasePosition;
    private bool hasActivePressBasePosition;
    private bool isVisuallyPressed;
    private Vector3 hoverScaleBaseScale = Vector3.one;
    private bool hasHoverScaleBaseScale;
    private bool isPointerInside;
    private bool persistentSelected;

    protected override void Awake()
    {
        base.Awake();
        rectTransform = transform as RectTransform;
        CacheHoverScaleBaseScale();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheHoverScaleBaseScale();
        RefreshVisualState(instant: true);
        UpdateHoverScale(instant: true);
    }

    protected override void OnDisable()
    {
        KillPressTween();
        KillHoverScaleTween();

        if (rectTransform != null && hasActivePressBasePosition)
        {
            rectTransform.anchoredPosition = activePressBasePosition;
        }

        RectTransform resolvedHoverScaleTarget = ResolveHoverScaleTarget();
        if (resolvedHoverScaleTarget != null && hasHoverScaleBaseScale)
            resolvedHoverScaleTarget.localScale = hoverScaleBaseScale;

        hasActivePressBasePosition = false;
        isVisuallyPressed = false;
        hasHoverScaleBaseScale = false;
        isPointerInside = false;
        base.OnDisable();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        SelectionState visualState = ResolveVisualState(state);

        // Preserve existing button behavior
        base.DoStateTransition(visualState, instant);

        ApplyExtraImageTint(visualState, instant);
        HandlePressAnimation(state, instant);
        HandleClickSound(state, instant);
        UpdateHoverScale(instant);
    }

    public bool PersistentSelected => persistentSelected;

    public void SetPersistentSelected(bool selected, bool instant = false)
    {
        if (persistentSelected == selected)
            return;

        persistentSelected = selected;
        RefreshVisualState(instant);
    }

    public void RefreshVisualState(bool instant = false)
    {
        DoStateTransition(currentSelectionState, instant);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        isPointerInside = true;
        UpdateHoverScale(instant: false);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        isPointerInside = false;
        UpdateHoverScale(instant: false);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        UpdateHoverScale(instant: false);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        UpdateHoverScale(instant: false);
    }

    private void HandleClickSound(SelectionState state, bool instant)
    {
        if (state == SelectionState.Pressed && !string.IsNullOrEmpty(clickSound))
        {
            App.Sound.PlaySound(clickSound, true);
        }
    }

    private void ApplyExtraImageTint(SelectionState state, bool instant)
    {
        // Only apply color tints when the button's transition is ColorTint
        if (transition != Transition.ColorTint || extraImages == null || extraImages.Length == 0)
            return;

        var cb = colors; // ColorBlock
        Color tintColor;
        switch (state)
        {
            case SelectionState.Normal:
                tintColor = cb.normalColor;
                break;
            case SelectionState.Highlighted:
                tintColor = cb.highlightedColor;
                break;
            case SelectionState.Pressed:
                tintColor = cb.pressedColor;
                break;
            case SelectionState.Disabled:
                tintColor = cb.disabledColor;
                break;
            case SelectionState.Selected:
                tintColor = cb.selectedColor;
                break;
            default:
                tintColor = cb.normalColor;
                break;
        }

        float duration = instant ? 0f : cb.fadeDuration;
        for (int i = 0; i < extraImages.Length; i++)
        {
            var img = extraImages[i];
            if (img == null) continue;
            img.CrossFadeColor(tintColor, duration, true, true);
        }
    }

    private void HandlePressAnimation(SelectionState state, bool instant)
    {
        if (rectTransform == null)
            return;

        bool shouldBePressed = state == SelectionState.Pressed;

        if (instant)
        {
            ApplyInstantPressState(shouldBePressed);
            return;
        }

        if (shouldBePressed)
        {
            AnimateToPressedState();
            return;
        }

        AnimateToReleasedState();
    }

    private void AnimateToPressedState()
    {
        if (isVisuallyPressed)
            return;

        activePressBasePosition = rectTransform.anchoredPosition;
        hasActivePressBasePosition = true;
        isVisuallyPressed = true;
        Vector2 pressedAnchoredPosition = activePressBasePosition + (Vector2.down * pressOffset);

        KillPressTween();
        pressTween = rectTransform.DOAnchorPos(pressedAnchoredPosition, pressDownDuration)
            .SetEase(pressDownEase)
            .SetTarget(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() => pressTween = null);
    }

    private void AnimateToReleasedState()
    {
        if (!isVisuallyPressed || !hasActivePressBasePosition)
            return;

        isVisuallyPressed = false;
        KillPressTween();
        pressTween = rectTransform.DOAnchorPos(activePressBasePosition, pressUpDuration)
            .SetEase(pressUpEase)
            .SetTarget(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() => pressTween = null)
            .OnComplete(() => hasActivePressBasePosition = false);
    }

    private void ApplyInstantPressState(bool shouldBePressed)
    {
        KillPressTween();

        if (shouldBePressed)
        {
            if (!isVisuallyPressed)
            {
                activePressBasePosition = rectTransform.anchoredPosition;
                hasActivePressBasePosition = true;
            }

            rectTransform.anchoredPosition = activePressBasePosition + (Vector2.down * pressOffset);
            isVisuallyPressed = true;
            return;
        }

        if (isVisuallyPressed && hasActivePressBasePosition)
        {
            rectTransform.anchoredPosition = activePressBasePosition;
        }

        hasActivePressBasePosition = false;
        isVisuallyPressed = false;
    }

    private void UpdateHoverScale(bool instant)
    {
        RectTransform resolvedHoverScaleTarget = ResolveHoverScaleTarget();
        if (resolvedHoverScaleTarget == null)
            return;

        if (!enableHoverScale)
        {
            if (!hasHoverScaleBaseScale)
                CacheHoverScaleBaseScale();
            ApplyHoverScale(resolvedHoverScaleTarget, hoverScaleBaseScale, instant: true);
            return;
        }

        if (!hasHoverScaleBaseScale)
            CacheHoverScaleBaseScale();

        bool shouldBeScaled = isPointerInside && IsActive() && IsInteractable();
        Vector3 targetScale = shouldBeScaled
            ? hoverScaleBaseScale * hoverScaleMultiplier
            : hoverScaleBaseScale;
        ApplyHoverScale(resolvedHoverScaleTarget, targetScale, instant);
    }

    private SelectionState ResolveVisualState(SelectionState state)
    {
        if (!persistentSelected)
            return state;

        if (state == SelectionState.Disabled || state == SelectionState.Pressed)
            return state;

        return SelectionState.Selected;
    }

    private void ApplyHoverScale(RectTransform target, Vector3 targetScale, bool instant)
    {
        KillHoverScaleTween();

        if (instant || hoverScaleDuration <= 0f)
        {
            target.localScale = targetScale;
            return;
        }

        hoverScaleTween = target.DOScale(targetScale, hoverScaleDuration)
            .SetEase(hoverScaleEase)
            .SetTarget(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() => hoverScaleTween = null);
    }

    private RectTransform ResolveHoverScaleTarget()
    {
        if (hoverScaleTarget != null)
            return hoverScaleTarget;

        return rectTransform != null ? rectTransform : transform as RectTransform;
    }

    private void CacheHoverScaleBaseScale()
    {
        RectTransform resolvedHoverScaleTarget = ResolveHoverScaleTarget();
        if (resolvedHoverScaleTarget == null)
            return;

        hoverScaleBaseScale = resolvedHoverScaleTarget.localScale;
        hasHoverScaleBaseScale = true;
    }

    private void KillPressTween()
    {
        if (pressTween == null || !pressTween.IsActive())
            return;

        pressTween.Kill();
        pressTween = null;
    }

    private void KillHoverScaleTween()
    {
        if (hoverScaleTween == null || !hoverScaleTween.IsActive())
            return;

        hoverScaleTween.Kill();
        hoverScaleTween = null;
    }
}
