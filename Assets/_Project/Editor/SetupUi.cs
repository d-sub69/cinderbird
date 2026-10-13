using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.EditorTools
{
    /// <summary>
    /// Builders for plain uGUI menus in setup commands: panels, labels, buttons, sliders, toggles, dropdowns and
    /// label-control rows, using Unity's built-in UI sprites so controls look right without any art.
    /// </summary>
    internal static class SetupUi
    {
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color PanelColor = new Color(0.12f, 0.1f, 0.09f, 0.95f);
        public static readonly Color ButtonColor = new Color(0.93f, 0.9f, 0.86f, 1f);
        public static readonly Color DarkText = new Color(0.15f, 0.12f, 0.1f, 1f);
        public static readonly Color LightText = new Color(0.95f, 0.92f, 0.88f, 1f);

        public const float RowHeight = 52f;
        public const float FontSize = 28f;

        // The same sprites Unity's GameObject → UI menu uses.
        public static TMP_DefaultControls.Resources TmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = BuiltinSprite("UI/Skin/UISprite.psd"),
                background = BuiltinSprite("UI/Skin/Background.psd"),
                inputField = BuiltinSprite("UI/Skin/InputFieldBackground.psd"),
                knob = BuiltinSprite("UI/Skin/Knob.psd"),
                checkmark = BuiltinSprite("UI/Skin/Checkmark.psd"),
                dropdown = BuiltinSprite("UI/Skin/DropdownArrow.psd"),
                mask = BuiltinSprite("UI/Skin/UIMask.psd"),
            };
        }

        public static DefaultControls.Resources UiResources()
        {
            return new DefaultControls.Resources
            {
                standard = BuiltinSprite("UI/Skin/UISprite.psd"),
                background = BuiltinSprite("UI/Skin/Background.psd"),
                inputField = BuiltinSprite("UI/Skin/InputFieldBackground.psd"),
                knob = BuiltinSprite("UI/Skin/Knob.psd"),
                checkmark = BuiltinSprite("UI/Skin/Checkmark.psd"),
                dropdown = BuiltinSprite("UI/Skin/DropdownArrow.psd"),
                mask = BuiltinSprite("UI/Skin/UIMask.psd"),
            };
        }

        public static GameObject CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject created = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            created.transform.SetParent(parent, false);
            created.GetComponent<Image>().color = color;
            return created;
        }

        public static TextMeshProUGUI CreateLabel(string objectName, Transform parent, string text, float size, Color color,
            float height, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject created = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            TextMeshProUGUI label = created.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            created.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        public static Button CreateButton(string objectName, Transform parent, string text)
        {
            GameObject buttonObject = CreateImage(objectName, parent, ButtonColor);
            Button button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            TextMeshProUGUI label = CreateLabel("Text", buttonObject.transform, text, FontSize, DarkText, 64f);
            Stretch(label.gameObject);
            return button;
        }

        /// <summary>A dimmed full-screen cover holding a centered panel with a vertical layout; returns the panel.</summary>
        public static GameObject CreateDialog(string objectName, Transform parent, Vector2 size, out GameObject cover)
        {
            cover = CreateImage(objectName, parent, DimColor);
            Stretch(cover);
            GameObject panel = CreateImage("Panel", cover.transform, PanelColor);
            Center(panel, size);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 32, 32);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        /// <summary>A horizontal row: a fixed-width label on the left, then whatever is added to the returned row.</summary>
        public static Transform CreateRow(string label, Transform parent, float labelWidth = 340f)
        {
            GameObject row = new GameObject(label + " Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = RowHeight;

            TextMeshProUGUI text = CreateLabel("Label", row.transform, label, FontSize, LightText, RowHeight, TextAlignmentOptions.MidlineLeft);
            text.GetComponent<LayoutElement>().preferredWidth = labelWidth;
            return row.transform;
        }

        public static Slider CreateSlider(Transform row)
        {
            GameObject sliderObject = DefaultControls.CreateSlider(UiResources());
            sliderObject.transform.SetParent(row, false);
            LayoutElement layout = sliderObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 28f;
            return sliderObject.GetComponent<Slider>();
        }

        /// <summary>A small right-hand read-out next to a slider (e.g. "1.00×").</summary>
        public static TextMeshProUGUI CreateValueLabel(Transform row)
        {
            TextMeshProUGUI value = CreateLabel("Value", row, "-", FontSize, LightText, RowHeight, TextAlignmentOptions.MidlineRight);
            value.GetComponent<LayoutElement>().preferredWidth = 110f;
            return value;
        }

        public static Toggle CreateToggle(Transform row)
        {
            GameObject toggleObject = DefaultControls.CreateToggle(UiResources());
            toggleObject.transform.SetParent(row, false);
            // The default toggle has its own small legacy-text label; the row already has one.
            Transform builtInLabel = toggleObject.transform.Find("Label");
            if (builtInLabel != null)
            {
                Object.DestroyImmediate(builtInLabel.gameObject);
            }

            RectTransform background = (RectTransform)toggleObject.transform.Find("Background");
            background.anchorMin = new Vector2(0f, 0.5f);
            background.anchorMax = new Vector2(0f, 0.5f);
            background.pivot = new Vector2(0f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(36f, 36f);
            RectTransform checkmark = (RectTransform)background.Find("Checkmark");
            checkmark.anchorMin = Vector2.zero;
            checkmark.anchorMax = Vector2.one;
            checkmark.offsetMin = Vector2.zero;
            checkmark.offsetMax = Vector2.zero;

            LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 36f;
            layout.preferredHeight = 36f;
            return toggleObject.GetComponent<Toggle>();
        }

        public static TMP_Dropdown CreateDropdown(Transform row)
        {
            GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(TmpResources());
            dropdownObject.transform.SetParent(row, false);
            TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.captionText.fontSize = FontSize - 4f;
            dropdown.itemText.fontSize = FontSize - 4f;

            // The default list is sized for 14-pt text; make rows and the open list fit the larger font.
            RectTransform template = dropdown.template;
            template.sizeDelta = new Vector2(template.sizeDelta.x, 320f);
            RectTransform content = (RectTransform)template.Find("Viewport/Content");
            content.sizeDelta = new Vector2(content.sizeDelta.x, 44f);
            RectTransform item = (RectTransform)content.Find("Item");
            item.sizeDelta = new Vector2(item.sizeDelta.x, 44f);

            LayoutElement layout = dropdownObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 44f;
            return dropdown;
        }

        public static void Stretch(GameObject target)
        {
            RectTransform rect = (RectTransform)target.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Center(GameObject target, Vector2 size)
        {
            RectTransform rect = (RectTransform)target.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static Sprite BuiltinSprite(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }
    }
}
