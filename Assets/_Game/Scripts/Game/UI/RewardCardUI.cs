using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    public sealed class RewardCardUI : MonoBehaviour
    {
        private static Sprite _defaultSprite;
        private static bool _didResolveDefaultSprite;

        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _rarityBorderImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _amountText;

        public void Bind(ResolvedReward reward)
        {
            bool hasReward = reward.RewardData != null;

            if (_iconImage != null)
            {
                _iconImage.enabled = hasReward && reward.RewardData.Icon != null;
                _iconImage.sprite = hasReward ? reward.RewardData.Icon : null;
            }

            if (_rarityBorderImage != null)
                _rarityBorderImage.color = hasReward ? RewardRarityColorUtility.GetColor(reward.Rarity) : Color.white;

            if (_nameText != null)
                _nameText.text = hasReward ? reward.RewardName : string.Empty;

            if (_amountText != null)
                _amountText.text = hasReward ? reward.FormatAmountLabel() : string.Empty;
        }

        public static RewardCardUI CreateDefault(Transform parent)
        {
            GameObject root = new GameObject("RewardCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup), typeof(RewardCardUI));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.localScale = Vector3.one;

            Image background = root.GetComponent<Image>();
            ApplyDefaultGraphic(background, new Color(1f, 1f, 1f, 0.05f));

            LayoutElement layoutElement = root.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 92f;
            layoutElement.flexibleWidth = 1f;

            HorizontalLayoutGroup layoutGroup = root.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(18, 18, 14, 14);
            layoutGroup.spacing = 14;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            GameObject accentObject = CreateGraphic("RarityAccent", root.transform, new Color(1f, 1f, 1f, 0.18f));
            RectTransform accentRect = accentObject.GetComponent<RectTransform>();
            accentRect.sizeDelta = new Vector2(6f, 0f);
            LayoutElement accentLayout = accentObject.AddComponent<LayoutElement>();
            accentLayout.preferredWidth = 6f;
            accentLayout.flexibleHeight = 1f;

            GameObject iconFrameObject = CreateGraphic("IconFrame", root.transform, new Color(1f, 1f, 1f, 0.08f));
            RectTransform iconFrameRect = iconFrameObject.GetComponent<RectTransform>();
            iconFrameRect.sizeDelta = new Vector2(64f, 64f);
            LayoutElement iconFrameLayout = iconFrameObject.AddComponent<LayoutElement>();
            iconFrameLayout.preferredWidth = 64f;
            iconFrameLayout.preferredHeight = 64f;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(iconFrameObject.transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10f, 10f);
            iconRect.offsetMax = new Vector2(-10f, -10f);

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;

            GameObject textColumnObject = new GameObject("TextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            RectTransform textColumnRect = textColumnObject.GetComponent<RectTransform>();
            textColumnRect.SetParent(root.transform, false);
            LayoutElement textColumnLayout = textColumnObject.GetComponent<LayoutElement>();
            textColumnLayout.flexibleWidth = 1f;

            VerticalLayoutGroup textColumnGroup = textColumnObject.GetComponent<VerticalLayoutGroup>();
            textColumnGroup.spacing = 4;
            textColumnGroup.childAlignment = TextAnchor.MiddleLeft;
            textColumnGroup.childControlWidth = true;
            textColumnGroup.childControlHeight = true;
            textColumnGroup.childForceExpandWidth = true;
            textColumnGroup.childForceExpandHeight = false;

            TextMeshProUGUI nameText = CreateText("NameText", textColumnObject.transform, 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            nameText.color = Color.white;

            TextMeshProUGUI amountText = CreateText("AmountText", textColumnObject.transform, 16f, FontStyles.Normal, TextAlignmentOptions.Left);
            amountText.color = new Color(1f, 1f, 1f, 0.7f);

            RewardCardUI view = root.GetComponent<RewardCardUI>();
            view._iconImage = iconImage;
            view._rarityBorderImage = accentObject.GetComponent<Image>();
            view._nameText = nameText;
            view._amountText = amountText;

            return view;
        }

        public static Sprite GetDefaultSprite()
        {
            if (_didResolveDefaultSprite)
                return _defaultSprite;

            _didResolveDefaultSprite = true;
            _defaultSprite = null;

            return _defaultSprite;
        }

        public static void ApplyDefaultGraphic(Image image, Color color)
        {
            if (image == null)
                return;

            Sprite sprite = GetDefaultSprite();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
        }

        private static GameObject CreateGraphic(string name, Transform parent, Color color)
        {
            GameObject graphicObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            Image image = graphicObject.GetComponent<Image>();
            ApplyDefaultGraphic(image, color);

            return graphicObject;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text.font == null)
                text.font = TMP_Settings.defaultFontAsset;

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;

            return text;
        }
    }
}
