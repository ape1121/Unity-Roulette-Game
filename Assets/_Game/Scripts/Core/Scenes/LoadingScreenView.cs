using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class LoadingScreenView : MonoBehaviour
{
    private const float EnterDuration = 0.35f;
    private const float ExitDuration = 0.30f;
    private const float SpinnerRotationDuration = 1.1f;
    private const string DefaultStatus = "Loading";

    [Header("Structure")]
    [SerializeField] private RectTransform _rootRect;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasScaler _canvasScaler;
    [SerializeField] private GraphicRaycaster _graphicRaycaster;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Animation")]
    [SerializeField] private RectTransform _panelRect;
    [SerializeField] private RectTransform _spinnerRect;

    [Header("Content")]
    [SerializeField] private Image _progressFillImage;
    [SerializeField] private TextMeshProUGUI _statusText;

    private Tween _spinnerTween;
    private bool _isInitialized;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        ValidateSerializedReferences();
        ConfigureRootComponents();
        _statusText.text = DefaultStatus;
        _progressFillImage.fillAmount = 0f;
        SetVisible(false);
        _isInitialized = true;
    }

    public void SetStatus(string status)
    {
        Initialize();
        _statusText.text = string.IsNullOrWhiteSpace(status) ? DefaultStatus : status;
    }

    public void SetProgress(float progress)
    {
        Initialize();
        _progressFillImage.fillAmount = Mathf.Clamp01(progress);
    }

    public IEnumerator PlayEnterTransition(string status)
    {
        Initialize();
        SetStatus(status);
        SetProgress(0f);
        SetVisible(true);

        _canvasGroup.alpha = 0f;
        _panelRect.localScale = new Vector3(0.96f, 0.96f, 1f);
        StartSpinner();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(_canvasGroup.DOFade(1f, EnterDuration).SetEase(Ease.OutCubic));
        sequence.Join(_panelRect.DOScale(1f, EnterDuration).SetEase(Ease.OutBack));
        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayExitTransition()
    {
        Initialize();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(_canvasGroup.DOFade(0f, ExitDuration).SetEase(Ease.InCubic));
        sequence.Join(_panelRect.DOScale(1.03f, ExitDuration).SetEase(Ease.InCubic));
        yield return sequence.WaitForCompletion();

        StopSpinner();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        StopSpinner();
    }

    private void OnValidate()
    {
        _rootRect ??= GetComponent<RectTransform>();
        _canvas ??= GetComponent<Canvas>();
        _canvasScaler ??= GetComponent<CanvasScaler>();
        _graphicRaycaster ??= GetComponent<GraphicRaycaster>();
        _canvasGroup ??= GetComponent<CanvasGroup>();
    }

    private void ConfigureRootComponents()
    {
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasScaler.referenceResolution = new Vector2(1080f, 1920f);
        _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        _graphicRaycaster.ignoreReversedGraphics = true;
        _graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        _canvasGroup.interactable = false;
        _canvasGroup.ignoreParentGroups = true;
    }

    private void ValidateSerializedReferences()
    {
        if (_rootRect == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a root RectTransform reference.");

        if (_canvas == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a Canvas reference.");

        if (_canvasScaler == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a CanvasScaler reference.");

        if (_graphicRaycaster == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a GraphicRaycaster reference.");

        if (_canvasGroup == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a CanvasGroup reference.");

        if (_panelRect == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a panel RectTransform reference.");

        if (_spinnerRect == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a spinner RectTransform reference.");

        if (_progressFillImage == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a progress fill Image reference.");

        if (_statusText == null)
            throw new MissingReferenceException($"{nameof(LoadingScreenView)} requires a status TextMeshProUGUI reference.");
    }

    private void SetVisible(bool isVisible)
    {
        _rootRect.gameObject.SetActive(isVisible);
        _canvasGroup.blocksRaycasts = isVisible;
    }

    private void StartSpinner()
    {
        StopSpinner();
        _spinnerTween = _spinnerRect.DORotate(new Vector3(0f, 0f, -360f), SpinnerRotationDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetUpdate(true);
    }

    private void StopSpinner()
    {
        if (_spinnerTween != null && _spinnerTween.IsActive())
            _spinnerTween.Kill();

        _spinnerTween = null;
    }
}
