using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ArmWrestlingVR
{
    /// <summary>统一提供界面字体。优先加载随工程附带的中文字体，缺失时退回内置字体。</summary>
    public static class FontProvider
    {
        private const string ResourcePath = "ArmWrestlingFont";
        private static Font cached;

        public static Font UiFont
        {
            get
            {
                if (cached != null)
                {
                    return cached;
                }

                cached = Resources.Load<Font>(ResourcePath);
                if (cached == null)
                {
                    cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return cached;
            }
        }
    }

    /// <summary>uGUI 程序化构建工具。整个界面由代码生成，不依赖预制体与美术资源。</summary>
    public static class UiFactory
    {
        public const int UiLayer = 5;

        public static Canvas CreateCanvas(Transform parent, string name, Vector2 sizePixels, float pixelsPerMeter, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = localRotation;

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = sizePixels;

            float scale = pixelsPerMeter > 0f ? 1f / pixelsPerMeter : 0.001f;
            canvasObject.transform.localScale = Vector3.one * scale;

            return canvas;
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.layer = UiLayer;
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateLabel(Transform parent, string name, string content, int fontSize, Color color, TextAnchor anchor, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.layer = UiLayer;
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Text text = labelObject.AddComponent<Text>();
            text.font = FontProvider.UiFont;
            text.fontSize = fontSize;
            text.text = content;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static VrButton CreateButton(Transform parent, string name, string caption, Vector2 size, Vector2 anchoredPosition, int fontSize, Color tint)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.layer = UiLayer;
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image background = buttonObject.AddComponent<Image>();
            background.color = tint;
            background.raycastTarget = true;

            BoxCollider collider = buttonObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 14f);
            collider.center = Vector3.zero;

            Text label = CreateLabel(buttonObject.transform, "Label", caption, fontSize, Color.white, TextAnchor.MiddleCenter,
                new Vector2(size.x - 8f, size.y - 6f), Vector2.zero);

            VrButton button = buttonObject.AddComponent<VrButton>();
            button.background = background;
            button.label = label;
            button.normalColor = tint;
            button.highlightColor = Color.Lerp(tint, Color.white, 0.45f);
            button.disabledColor = new Color(tint.r * 0.45f, tint.g * 0.45f, tint.b * 0.45f, 0.75f);
            return button;
        }
    }

    /// <summary>可被手柄射线点击的按钮。自己实现，避免引入 EventSystem 与输入模块。</summary>
    [DisallowMultipleComponent]
    public class VrButton : MonoBehaviour
    {
        public Image background;
        public Text label;
        public Color normalColor = new Color(0.20f, 0.45f, 0.85f, 1f);
        public Color highlightColor = new Color(0.45f, 0.70f, 1f, 1f);
        public Color disabledColor = new Color(0.30f, 0.32f, 0.35f, 0.7f);
        public UnityEvent onClick = new UnityEvent();

        [SerializeField] private bool interactable = true;
        private bool hovered;

        public event Action Clicked;

        public bool Interactable
        {
            get { return interactable; }
            set
            {
                interactable = value;
                RefreshVisual();
            }
        }

        public string Caption
        {
            get { return label != null ? label.text : string.Empty; }
            set
            {
                if (label != null)
                {
                    label.text = value;
                }
            }
        }

        public void SetHovered(bool value)
        {
            if (hovered == value)
            {
                return;
            }

            hovered = value;
            RefreshVisual();
        }

        public void Press()
        {
            if (!interactable)
            {
                return;
            }

            if (onClick != null)
            {
                onClick.Invoke();
            }

            if (Clicked != null)
            {
                Clicked();
            }
        }

        private void RefreshVisual()
        {
            if (background == null)
            {
                return;
            }

            background.color = !interactable
                ? disabledColor
                : (hovered ? highlightColor : normalColor);
        }
    }

    /// <summary>
    /// 手柄射线指示器与点击器。
    ///
    /// XR 模式下射线来自右手柄（PICO 4 右手控制器），扳机按下即点击；
    /// 桌面模式下射线来自鼠标位置，左键点击，方便在编辑器里验证整套交互流程。
    /// </summary>
    public class VrPointer : MonoBehaviour
    {
        public XrPlayerRig rig;
        public Camera eyeCamera;
        public Transform xrRaySource;
        public float maxDistance = 4f;
        public float triggerThreshold = 0.6f;
        public Color lineColor = new Color(0.4f, 0.9f, 1f, 0.9f);

        private VrButton hoveredButton;
        private LineRenderer line;
        private bool lastTriggerDown;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            if (line == null)
            {
                line = gameObject.AddComponent<LineRenderer>();
            }

            line.positionCount = 2;
            line.startWidth = 0.004f;
            line.endWidth = 0.002f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }

            line.startColor = lineColor;
            line.endColor = lineColor;
        }

        private void Update()
        {
            Ray ray;
            bool hasRay = TryGetRay(out ray);
            bool triggerDown;

            if (hasRay)
            {
                UpdateRayVisual(ray);
                triggerDown = GetTriggerDown();
            }
            else
            {
                if (line != null)
                {
                    line.enabled = false;
                }

                SetHovered(null);
                triggerDown = false;
            }

            if (triggerDown && !lastTriggerDown && hoveredButton != null)
            {
                hoveredButton.Press();
            }

            lastTriggerDown = triggerDown;
        }

        private bool TryGetRay(out Ray ray)
        {
            if (rig != null && rig.XrActive)
            {
                Transform source = xrRaySource != null ? xrRaySource : rig.rightHand;
                if (source == null)
                {
                    ray = default(Ray);
                    return false;
                }

                ray = new Ray(source.position + source.forward * 0.02f, source.forward);
                return true;
            }

            Camera camera = eyeCamera != null ? eyeCamera : Camera.main;
            if (camera == null)
            {
                ray = default(Ray);
                return false;
            }

            ray = camera.ScreenPointToRay(Input.mousePosition);
            return true;
        }

        private void UpdateRayVisual(Ray ray)
        {
            RaycastHit hit;
            bool hitSomething = Physics.Raycast(ray, out hit, maxDistance, 1 << UiFactory.UiLayer);

            Vector3 end = hitSomething ? hit.point : ray.origin + ray.direction * maxDistance;

            if (line != null)
            {
                line.enabled = true;
                line.SetPosition(0, ray.origin + ray.direction * 0.02f);
                line.SetPosition(1, end);
            }

            VrButton button = null;
            if (hitSomething)
            {
                button = hit.collider.GetComponentInParent<VrButton>();
            }

            SetHovered(button);
        }

        private void SetHovered(VrButton button)
        {
            if (hoveredButton == button)
            {
                return;
            }

            if (hoveredButton != null)
            {
                hoveredButton.SetHovered(false);
            }

            hoveredButton = button;

            if (hoveredButton != null)
            {
                hoveredButton.SetHovered(true);
            }
        }

        private bool GetTriggerDown()
        {
            if (rig != null && rig.XrActive)
            {
                float value;
                if (rig.TryGetTrigger(XRNode.RightHand, out value))
                {
                    return value >= triggerThreshold;
                }

                return false;
            }

            return Input.GetMouseButton(0);
        }
    }
}
