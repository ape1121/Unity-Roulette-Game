using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ape.Game.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DynamicScrollableGrid : MonoBehaviour
    {
        private enum ScrollAxis
        {
            Vertical,
            Horizontal
        }

        [System.Serializable]
        private struct LayoutModeSettings
        {
            public ScrollAxis scrollDirection;

            [Min(1)]
            [Tooltip("For vertical scrolling this is the column count. For horizontal scrolling this is the row count.")]
            public int fitCount;

            [Tooltip("Cell width and height ratio expressed as X:Y.")]
            public Vector2 cellAspect;
        }

        [Header("References")]
        [SerializeField] private GridLayoutGroup gridLayoutGroup;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Mode Switching")]
        [Min(0.01f)] [SerializeField] private float landscapeAspectThreshold = 1f;
        [SerializeField] private LayoutModeSettings portraitLayout = new LayoutModeSettings
        {
            scrollDirection = ScrollAxis.Vertical,
            fitCount = 2,
            cellAspect = Vector2.one
        };
        [SerializeField] private LayoutModeSettings landscapeLayout = new LayoutModeSettings
        {
            scrollDirection = ScrollAxis.Horizontal,
            fitCount = 2,
            cellAspect = Vector2.one
        };

        [Header("Content")]
        [SerializeField] private bool resizeContentToFitGrid = true;

        private RectTransform _contentRect;
        private RectTransform _viewportRect;
        private Vector2 _lastViewportSize = new Vector2(-1f, -1f);
        private int _lastActiveChildCount = -1;
        private bool _lastWasLandscape;
        private bool _layoutDirty = true;
        private bool _hasAppliedLayout;
        private ScrollAxis _lastScrollAxis;
#if UNITY_EDITOR
        private bool _editorRefreshQueued;
#endif

        public void RefreshLayout()
        {
            RequestLayoutRefresh();
        }

        private void Reset()
        {
            CacheReferences();
            RefreshLayout();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            RequestLayoutRefresh();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            CancelEditorRefresh();
#endif
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
                ApplyLayoutIfNeeded(force: false);
        }

        private void OnTransformChildrenChanged()
        {
            RequestLayoutRefresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            RequestLayoutRefresh();
        }

        private void OnValidate()
        {
            SanitizeSettings();
            CacheReferences();
            RequestLayoutRefresh();
        }

        private void RequestLayoutRefresh()
        {
            _layoutDirty = true;

            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying)
            {
                ApplyLayoutIfNeeded(force: true);
                return;
            }

#if UNITY_EDITOR
            QueueEditorRefresh();
#endif
        }

        private void CacheReferences()
        {
            gridLayoutGroup ??= GetComponent<GridLayoutGroup>();
            scrollRect ??= GetComponentInParent<ScrollRect>();

            _contentRect = gridLayoutGroup != null
                ? gridLayoutGroup.transform as RectTransform
                : GetComponent<RectTransform>();

            if (scrollRect != null)
            {
                _viewportRect = scrollRect.viewport != null
                    ? scrollRect.viewport
                    : scrollRect.transform as RectTransform;

                if (scrollRect.content == null && _contentRect != null)
                    scrollRect.content = _contentRect;
            }
            else if (_contentRect != null && _contentRect.parent != null)
            {
                _viewportRect = _contentRect.parent as RectTransform;
            }
            else
            {
                _viewportRect = GetComponent<RectTransform>();
            }
        }

        private void ApplyLayoutIfNeeded(bool force)
        {
            if (!isActiveAndEnabled)
                return;

            SanitizeSettings();
            CacheReferences();

            if (gridLayoutGroup == null || _contentRect == null || _viewportRect == null)
                return;

            Vector2 viewportSize = _viewportRect.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
                return;

            bool isLandscape = (viewportSize.x / viewportSize.y) >= landscapeAspectThreshold;
            int activeChildCount = CountActiveLayoutChildren(_contentRect);

            if (!force
                && !_layoutDirty
                && Approximately(viewportSize, _lastViewportSize)
                && activeChildCount == _lastActiveChildCount
                && isLandscape == _lastWasLandscape)
            {
                return;
            }

            ApplyLayout(viewportSize, activeChildCount, isLandscape);

            _lastViewportSize = viewportSize;
            _lastActiveChildCount = activeChildCount;
            _lastWasLandscape = isLandscape;
            _layoutDirty = false;
        }

        private void ApplyLayout(Vector2 viewportSize, int activeChildCount, bool isLandscape)
        {
            LayoutModeSettings settings = isLandscape ? landscapeLayout : portraitLayout;
            float aspectRatio = ResolveAspectRatio(settings);

            float cellWidth;
            float cellHeight;
            int columns;
            int rows;

            if (settings.scrollDirection == ScrollAxis.Vertical)
            {
                columns = Mathf.Max(1, settings.fitCount);
                float availableWidth = viewportSize.x
                    - gridLayoutGroup.padding.left
                    - gridLayoutGroup.padding.right
                    - ((columns - 1) * gridLayoutGroup.spacing.x);

                cellWidth = Mathf.Max(0f, availableWidth / columns);
                cellHeight = cellWidth / aspectRatio;
                rows = activeChildCount > 0 ? Mathf.CeilToInt(activeChildCount / (float)columns) : 0;

                gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
                gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayoutGroup.constraintCount = columns;
            }
            else
            {
                rows = Mathf.Max(1, settings.fitCount);
                float availableHeight = viewportSize.y
                    - gridLayoutGroup.padding.top
                    - gridLayoutGroup.padding.bottom
                    - ((rows - 1) * gridLayoutGroup.spacing.y);

                cellHeight = Mathf.Max(0f, availableHeight / rows);
                cellWidth = cellHeight * aspectRatio;
                columns = activeChildCount > 0 ? Mathf.CeilToInt(activeChildCount / (float)rows) : 0;

                gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Vertical;
                gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                gridLayoutGroup.constraintCount = rows;
            }

            gridLayoutGroup.cellSize = new Vector2(cellWidth, cellHeight);

            if (scrollRect != null)
            {
                scrollRect.horizontal = settings.scrollDirection == ScrollAxis.Horizontal;
                scrollRect.vertical = settings.scrollDirection == ScrollAxis.Vertical;
            }

            if (resizeContentToFitGrid)
                ResizeContent(viewportSize, columns, rows, gridLayoutGroup.cellSize);

            LayoutRebuilder.MarkLayoutForRebuild(_contentRect);

            if (Application.isPlaying)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
#endif

            if (!_hasAppliedLayout || settings.scrollDirection != _lastScrollAxis)
            {
                ResetScrollPosition();
                _lastScrollAxis = settings.scrollDirection;
                _hasAppliedLayout = true;
            }
        }

        private void ResizeContent(Vector2 viewportSize, int columns, int rows, Vector2 cellSize)
        {
            float contentWidth = gridLayoutGroup.padding.left + gridLayoutGroup.padding.right;
            float contentHeight = gridLayoutGroup.padding.top + gridLayoutGroup.padding.bottom;

            if (columns > 0)
                contentWidth += (columns * cellSize.x) + ((columns - 1) * gridLayoutGroup.spacing.x);

            if (rows > 0)
                contentHeight += (rows * cellSize.y) + ((rows - 1) * gridLayoutGroup.spacing.y);

            _contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(viewportSize.x, contentWidth));
            _contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewportSize.y, contentHeight));
        }

        private void SanitizeSettings()
        {
            landscapeAspectThreshold = Mathf.Max(0.01f, landscapeAspectThreshold);
            SanitizeLayout(ref portraitLayout);
            SanitizeLayout(ref landscapeLayout);
        }

        private static void SanitizeLayout(ref LayoutModeSettings settings)
        {
            settings.fitCount = Mathf.Max(1, settings.fitCount);
            settings.cellAspect.x = Mathf.Max(0.01f, settings.cellAspect.x);
            settings.cellAspect.y = Mathf.Max(0.01f, settings.cellAspect.y);
        }

        private static float ResolveAspectRatio(LayoutModeSettings settings)
        {
            return Mathf.Max(0.01f, settings.cellAspect.x) / Mathf.Max(0.01f, settings.cellAspect.y);
        }

        private static int CountActiveLayoutChildren(RectTransform contentRect)
        {
            int count = 0;

            for (int i = 0; i < contentRect.childCount; i++)
            {
                RectTransform child = contentRect.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                LayoutElement layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null && layoutElement.ignoreLayout)
                    continue;

                count++;
            }

            return count;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }

        private void ResetScrollPosition()
        {
            if (scrollRect == null)
                return;

            scrollRect.StopMovement();
            scrollRect.horizontalNormalizedPosition = 0f;
            scrollRect.verticalNormalizedPosition = 1f;
        }

#if UNITY_EDITOR
        private void QueueEditorRefresh()
        {
            if (_editorRefreshQueued)
                return;

            _editorRefreshQueued = true;
            EditorApplication.delayCall -= HandleEditorRefresh;
            EditorApplication.delayCall += HandleEditorRefresh;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void CancelEditorRefresh()
        {
            if (!_editorRefreshQueued)
                return;

            EditorApplication.delayCall -= HandleEditorRefresh;
            _editorRefreshQueued = false;
        }

        private void HandleEditorRefresh()
        {
            EditorApplication.delayCall -= HandleEditorRefresh;
            _editorRefreshQueued = false;

            if (this == null || !isActiveAndEnabled)
                return;

            ApplyLayoutIfNeeded(force: true);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }
}
