using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RogueArena.Core;

namespace RogueArena.UI
{
    /// <summary>
    /// Procedural UI toolkit: sliced background sprites, fonts, text, buttons and
    /// panels built in code. Everything shares a tiny set of generated sprites so
    /// the whole UI costs a handful of kilobytes and zero authored assets.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelDark = new Color(.04f, .05f, .07f, .92f);
        public static readonly Color PanelMid = new Color(.09f, .11f, .15f, .95f);
        public static readonly Color Accent = new Color(.2f, .95f, 1f);
        public static readonly Color TextMain = new Color(.88f, .93f, .97f);
        public static readonly Color TextDim = new Color(.55f, .62f, .70f);
        public static readonly Color Danger = new Color(1f, .32f, .25f);
        public static readonly Color Good = new Color(.3f, 1f, .5f);
        public static readonly Color Gold = new Color(1f, .85f, .3f);

        static Font font;
        static Sprite panelSprite;
        static Sprite solidSprite;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>9-slice dark panel with a subtle border baked into the sprite.</summary>
        public static Sprite PanelSprite
        {
            get
            {
                if (panelSprite != null) return panelSprite;
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "UiPanel"
                };
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        bool border = x < 2 || y < 2 || x > 29 || y > 29;
                        bool inner = x < 4 || y < 4 || x > 27 || y > 27;
                        Color c = border ? new Color(.16f, .85f, 1f, .55f) : inner ? new Color(1f, 1f, 1f, .06f) : Color.clear;
                        texture.SetPixel(x, y, c);
                    }
                texture.Apply();
                panelSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32f, 0,
                    SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
                return panelSprite;
            }
        }

        /// <summary>Plain white 1x1 (tinted solid fills, bars, dividers).</summary>
        public static Sprite SolidSprite
        {
            get
            {
                if (solidSprite != null) return solidSprite;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "UiSolid" };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                solidSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
                return solidSprite;
            }
        }

        // ---------------------------------------------------------------- factories

        public static GameObject CreateCanvasRoot(string name, Transform parent, out Canvas canvas, out CanvasScaler scaler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        public static Text Label(Transform parent, string name, string text, int size,
            Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color ?? TextMain;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Image Panel(Transform parent, string name, Color tint, float insetBorderPixels = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = tint;
            return image;
        }

        public static Image Fill(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = SolidSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Text button with hover tint + click sound. Returns the button.</summary>
        public static Button Button(Transform parent, string name, string label, int size,
            Action onClick, Color? tint = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = tint ?? PanelMid;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = Label(go.transform, "Label", label, size, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;

            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.25f, 1.35f, 1.45f, 1f);
            colours.pressedColor = new Color(.75f, .85f, .95f, 1f);
            colours.disabledColor = new Color(.5f, .5f, .5f, .5f);
            button.colors = colours;
            button.transition = Selectable.Transition.ColorTint;

            button.onClick.AddListener(() =>
            {
                Services.Audio?.Play("ui_click", .8f);
                onClick?.Invoke();
            });

            // Hover audio.
            var trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => Services.Audio?.Play("ui_hover", .35f, 1.1f));
            trigger.triggers.Add(entry);

            return button;
        }

        public static Slider Slider(Transform parent, string name, float value01, Action<float> onChange)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var background = Fill(go.transform, "Background", new Color(.1f, .12f, .16f, .95f));
            background.rectTransform.anchorMin = new Vector2(0f, .5f);
            background.rectTransform.anchorMax = new Vector2(1f, .5f);
            background.rectTransform.sizeDelta = new Vector2(0f, 6f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillRect = fillArea.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = new Vector2(-4f, 6f);
            var fillImage = Fill(fillArea.transform, "Fill", Accent);
            fillImage.rectTransform.anchorMin = Vector2.zero;
            fillImage.rectTransform.anchorMax = new Vector2(1f, 1f);

            var handleArea = new GameObject("Handle Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleRect = handleArea.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.sizeDelta = new Vector2(-16f, 10f);
            var handle = Fill(handleArea.transform, "Handle", new Color(.8f, .92f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(14f, 10f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillImage.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value01;
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        /// <summary>Stretch a rect to its parent (optionally with padding).</summary>
        public static RectTransform Stretch(RectTransform rect, float? left = null, float? right = null,
            float? top = null, float? bottom = null)
        {
            rect.anchorMin = new Vector2(left.HasValue || right.HasValue ? 0f : .5f, bottom.HasValue || top.HasValue ? 0f : .5f);
            rect.anchorMax = new Vector2(right.HasValue || left.HasValue ? 1f : .5f, top.HasValue || bottom.HasValue ? 1f : .5f);
            rect.offsetMin = new Vector2(left ?? 0f, bottom ?? 0f);
            rect.offsetMax = new Vector2(-right ?? 0f, -top ?? 0f);
            return rect;
        }
    }
}
