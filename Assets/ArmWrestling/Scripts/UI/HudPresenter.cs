using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 世界空间操作面板。
    ///
    /// 面板固定在“装置坐标系”里、悬浮在桌面斜上方并朝向使用者，因此头转动时它留在
    /// 真实桌面上方，而不是跟着脑袋走（避免晕动）。所有文字由代码生成，方便后续换成
    /// 美术面板而不改逻辑。
    ///
    /// 字号按“角分辨率”定：面板距眼睛约 1.7 m，正文 34 px（3.4 cm）≈ 1.1°，
    /// 比照 1.5 cm 字高在 1.7 m 处的 0.5° 明显更大，戴着头盔能正常阅读。
    /// </summary>
    public class HudPresenter : MonoBehaviour
    {
        private const int CanvasWidth = 1200;
        private const int CanvasHeight = 660;

        public Canvas canvas;
        public Text headerText;
        public Text statusText;
        public Text stateText;
        public Text metricsText;
        public Text hintText;
        public Text bigText;
        public Image bigBackdrop;
        public Text roleHeaderText;
        public Text strengthLabel;
        public VrButton primaryButton;
        public VrButton secondaryButton;
        public VrButton tertiaryButton;
        public VrButton strengthDownButton;
        public VrButton strengthUpButton;
        public VrButton[] roleButtons = new VrButton[3];
        private GameObject presentationGroup;
        internal VrButton appearanceNext, arenaNext;

        [Header("标定微调工具（仅标定页显示）")]
        public GameObject roleGroup;
        public GameObject calibrationGroup;
        public Text calibrationHint;

        public event Action<OpponentRole> RoleSelected;
        public event Action<int> StrengthStepped;
        public event Action<int, int> NudgeRequested;
        public event Action RecenterRequested;
        public event Action SavePoseRequested;

        private static readonly Color BackgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.90f);
        private static readonly Color HeaderColor = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color BodyColor = new Color(0.88f, 0.92f, 0.96f, 1f);
        private static readonly Color AccentColor = new Color(0.20f, 0.52f, 0.92f, 1f);
        private static readonly Color DangerColor = new Color(0.80f, 0.30f, 0.25f, 1f);

        private OpponentRole highlightedRole = OpponentRole.Balanced;
        private readonly StringBuilder builder = new StringBuilder(512);

        /// <summary>由场景生成工具调用：在给定父节点下创建整个世界空间面板。</summary>
        public void Build(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            canvas = UiFactory.CreateCanvas(parent, "HUD Canvas", new Vector2(CanvasWidth, CanvasHeight), 1000f, localPosition, localRotation);
            Transform canvasTransform = canvas.transform;

            UiFactory.CreatePanel(canvasTransform, "Background", new Vector2(CanvasWidth, CanvasHeight), Vector2.zero, BackgroundColor);

            headerText = UiFactory.CreateLabel(canvasTransform, "Header", "扳手腕 · VR 端",
                54, HeaderColor, TextAnchor.MiddleLeft, new Vector2(820f, 70f), new Vector2(-170f, 292f));

            statusText = UiFactory.CreateLabel(canvasTransform, "Status", "", 28, BodyColor, TextAnchor.MiddleLeft,
                new Vector2(1100f, 38f), new Vector2(0f, 238f));

            stateText = UiFactory.CreateLabel(canvasTransform, "State", "", 36, BodyColor, TextAnchor.MiddleRight,
                new Vector2(330f, 50f), new Vector2(420f, 292f));

            // 左栏：实时数据
            UiFactory.CreatePanel(canvasTransform, "Metrics Panel", new Vector2(560f, 380f), new Vector2(-296f, 30f), new Color(0f, 0f, 0f, 0.25f));
            metricsText = UiFactory.CreateLabel(canvasTransform, "Metrics", "", 31, BodyColor, TextAnchor.UpperLeft,
                new Vector2(520f, 344f), new Vector2(-296f, 30f));
            metricsText.lineSpacing = 1.12f;

            // 右栏：角色与强度（或标定微调工具，二者互斥显示）
            UiFactory.CreatePanel(canvasTransform, "Role Panel", new Vector2(400f, 400f), new Vector2(376f, 10f), new Color(0f, 0f, 0f, 0.25f));

            roleGroup = CreateGroup(canvasTransform, "Role Group");
            calibrationGroup = CreateGroup(canvasTransform, "Calibration Group");

            BuildRoleGroup((RectTransform)roleGroup.transform);
            BuildPresentationGroup(canvasTransform);
            BuildCalibrationGroup((RectTransform)calibrationGroup.transform);

            hintText = UiFactory.CreateLabel(canvasTransform, "Hint", "", 26, BodyColor, TextAnchor.MiddleCenter,
                new Vector2(1140f, 36f), new Vector2(0f, -310f));

            bigBackdrop = UiFactory.CreatePanel(canvasTransform, "Big Backdrop", new Vector2(1160f, 240f),
                new Vector2(0f, 10f), new Color(0.02f, 0.03f, 0.06f, 0.94f));

            bigText = UiFactory.CreateLabel(canvasTransform, "Big", "", 190, Color.white, TextAnchor.MiddleCenter,
                new Vector2(1100f, 200f), new Vector2(0f, 10f));
            bigText.fontStyle = FontStyle.Bold;

            primaryButton = UiFactory.CreateButton(canvasTransform, "Primary", "开始", new Vector2(340f, 70f),
                new Vector2(-376f, -248f), 36, AccentColor);
            secondaryButton = UiFactory.CreateButton(canvasTransform, "Secondary", "次要", new Vector2(340f, 70f),
                new Vector2(0f, -248f), 36, new Color(0.32f, 0.40f, 0.52f, 1f));
            tertiaryButton = UiFactory.CreateButton(canvasTransform, "Tertiary", "重新标定", new Vector2(340f, 70f),
                new Vector2(376f, -248f), 36, new Color(0.32f, 0.40f, 0.52f, 1f));

            SetBig(string.Empty);
            SetCalibrationToolsVisible(false);
        }

        private static GameObject CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name, typeof(RectTransform));
            group.layer = UiFactory.UiLayer;
            group.transform.SetParent(parent, false);

            RectTransform rect = group.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, 400f);
            rect.anchoredPosition = Vector2.zero;
            return group;
        }

        private void BuildRoleGroup(RectTransform parent)
        {
            roleHeaderText = UiFactory.CreateLabel(parent, "Role Header", "对抗策略", 34, HeaderColor,
                TextAnchor.MiddleCenter, new Vector2(360f, 46f), new Vector2(376f, 168f));

            string[] roleNames = { "均衡型", "防守型", "节奏型" };
            for (int i = 0; i < roleButtons.Length; i++)
            {
                int index = i;
                VrButton button = UiFactory.CreateButton(parent, "Role " + i, roleNames[i],
                    new Vector2(300f, 58f), new Vector2(376f, 100f - i * 70f), 34, AccentColor);
                button.Clicked += () =>
                {
                    highlightedRole = (OpponentRole)(index + 1);
                    if (RoleSelected != null)
                    {
                        RoleSelected(highlightedRole);
                    }
                };
                roleButtons[i] = button;
            }

            strengthDownButton = UiFactory.CreateButton(parent, "Strength Down", "－", new Vector2(72f, 58f),
                new Vector2(300f, -128f), 38, AccentColor);
            strengthDownButton.Clicked += () => { if (StrengthStepped != null) StrengthStepped(-1); };

            strengthUpButton = UiFactory.CreateButton(parent, "Strength Up", "＋", new Vector2(72f, 58f),
                new Vector2(452f, -128f), 38, AccentColor);
            strengthUpButton.Clicked += () => { if (StrengthStepped != null) StrengthStepped(1); };

            strengthLabel = UiFactory.CreateLabel(parent, "Strength", "力量 100%", 28, BodyColor,
                TextAnchor.MiddleCenter, new Vector2(118f, 46f), new Vector2(376f, -128f));
        }

        private void BuildPresentationGroup(Transform canvasTransform)
        {
            presentationGroup = CreateGroup(canvasTransform, "Presentation Choices");
            var parent = presentationGroup.transform;
            var size = new Vector2(480f, 70f);
            UiFactory.CreateLabel(parent, "Appearance Label", "对手外观", 30, HeaderColor,
                TextAnchor.MiddleLeft, size, new Vector2(-296f, 164f));
            appearanceNext = UiFactory.CreateButton(parent, "AppearanceNext", "", size,
                new Vector2(-296f, 94f), 36, AccentColor);
            UiFactory.CreateLabel(parent, "Venue Label", "对战场景", 30, HeaderColor,
                TextAnchor.MiddleLeft, size, new Vector2(-296f, -4f));
            arenaNext = UiFactory.CreateButton(parent, "ArenaNext", "", size,
                new Vector2(-296f, -74f), 36, AccentColor);
            presentationGroup.SetActive(false);
        }

        internal void ShowPresentationChoices(bool visible)
        {
            // Older baked scenes may still contain the original HUD.
            if (presentationGroup == null) return;
            presentationGroup.SetActive(visible);
            metricsText.gameObject.SetActive(!visible);
        }

        /// <summary>
        /// 标定页的微调按钮组：没有定位夹具时，用这 8 个箭头把虚拟装置推到真实装置的位置上。
        /// 每次点击移动 2 cm / 旋转 3°，最后按“保存位置”写入 PlayerPrefs。
        /// </summary>
        private void BuildCalibrationGroup(RectTransform parent)
        {
            UiFactory.CreateLabel(parent, "Calibration Header", "位置微调", 34, HeaderColor,
                TextAnchor.MiddleCenter, new Vector2(360f, 46f), new Vector2(376f, 168f));

            CreateNudgeButton(parent, "左移", new Vector2(250f, 104f), 0, -1);
            CreateNudgeButton(parent, "前进", new Vector2(376f, 104f), 1, 1);
            CreateNudgeButton(parent, "右移", new Vector2(502f, 104f), 0, 1);

            CreateNudgeButton(parent, "下降", new Vector2(250f, 34f), 2, -1);
            CreateNudgeButton(parent, "后移", new Vector2(376f, 34f), 1, -1);
            CreateNudgeButton(parent, "上升", new Vector2(502f, 34f), 2, 1);

            CreateNudgeButton(parent, "左转", new Vector2(250f, -36f), 3, -1);
            VrButton recenter = UiFactory.CreateButton(parent, "Recenter", "重对齐", new Vector2(118f, 62f),
                new Vector2(376f, -36f), 30, new Color(0.32f, 0.40f, 0.52f, 1f));
            recenter.Clicked += () => { if (RecenterRequested != null) RecenterRequested(); };

            CreateNudgeButton(parent, "右转", new Vector2(502f, -36f), 3, 1);

            VrButton save = UiFactory.CreateButton(parent, "Save Pose", "保存位置", new Vector2(370f, 62f),
                new Vector2(376f, -116f), 32, new Color(0.18f, 0.55f, 0.30f, 1f));
            save.Clicked += () => { if (SavePoseRequested != null) SavePoseRequested(); };
        }

        private void CreateNudgeButton(RectTransform parent, string caption, Vector2 position, int axis, int direction)
        {
            VrButton button = UiFactory.CreateButton(parent, "Nudge " + caption, caption,
                new Vector2(118f, 62f), position, 30, AccentColor);
            button.Clicked += () =>
            {
                if (NudgeRequested != null)
                {
                    NudgeRequested(axis, direction);
                }
            };
        }

        public void SetCalibrationToolsVisible(bool visible)
        {
            if (roleGroup != null)
            {
                roleGroup.SetActive(!visible);
            }

            if (calibrationGroup != null)
            {
                calibrationGroup.SetActive(visible);
            }
        }

        public void SetHeader(string text)
        {
            if (headerText != null)
            {
                headerText.text = text;
            }
        }

        public void SetState(string text, bool alarm)
        {
            if (stateText != null)
            {
                stateText.text = text;
                stateText.color = alarm ? DangerColor : BodyColor;
            }
        }

        public void SetStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }

        public void SetMetrics(string text)
        {
            if (metricsText != null)
            {
                metricsText.text = text;
            }
        }

        public void SetHint(string text)
        {
            if (hintText != null)
            {
                hintText.text = text;
            }
        }

        public void SetBig(string text)
        {
            bool visible = !string.IsNullOrEmpty(text);

            if (bigText != null)
            {
                bigText.text = text;
                bigText.enabled = visible;
            }

            if (bigBackdrop != null)
            {
                bigBackdrop.enabled = visible;
            }
        }

        public void SetStrength(float normalized)
        {
            if (strengthLabel != null)
            {
                strengthLabel.text = "力量 " + Mathf.RoundToInt(normalized * 100f) + "%";
            }
        }

        public void SetRoleHighlight(OpponentRole role)
        {
            highlightedRole = role;
            for (int i = 0; i < roleButtons.Length; i++)
            {
                if (roleButtons[i] == null)
                {
                    continue;
                }

                bool selected = (byte)(i + 1) == (byte)role;
                roleButtons[i].normalColor = selected ? new Color(0.95f, 0.62f, 0.15f, 1f) : AccentColor;
                roleButtons[i].SetHovered(false);
            }
        }

        public void SetRoleInteractable(bool value)
        {
            for (int i = 0; i < roleButtons.Length; i++)
            {
                if (roleButtons[i] != null)
                {
                    roleButtons[i].Interactable = value;
                }
            }
        }

        public void SetButtons(string primaryCaption, Action primaryAction, string secondaryCaption, Action secondaryAction,
            string tertiaryCaption, Action tertiaryAction)
        {
            ConfigureButton(primaryButton, primaryCaption, primaryAction);
            ConfigureButton(secondaryButton, secondaryCaption, secondaryAction);
            ConfigureButton(tertiaryButton, tertiaryCaption, tertiaryAction);
        }

        private static void ConfigureButton(VrButton button, string caption, Action action)
        {
            if (button == null)
            {
                return;
            }

            bool visible = !string.IsNullOrEmpty(caption);
            button.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            button.Caption = caption;
            // 没有绑定动作的按钮显示为禁用态，避免“点了没反应”的疑惑。
            button.Interactable = action != null;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() => action());
            }
        }

        /// <summary>生成左栏的多行数据文本。</summary>
        public string FormatMetrics(
            float mechanicalAngle,
            float visualAngle,
            float angularVelocity,
            float torque,
            float dataAgeSeconds,
            string sourceName,
            LinkStatus linkStatus,
            string linkDetail,
            DeviceState deviceState)
        {
            builder.Length = 0;
            builder.Append("机械角度    ").Append(mechanicalAngle.ToString("+0.0;-0.0;0.0")).Append("°\n");
            builder.Append("虚拟旋转    ").Append(visualAngle.ToString("+0.0;-0.0;0.0")).Append("°\n");
            builder.Append("角速度      ").Append(angularVelocity.ToString("0.0")).Append(" °/s\n");
            builder.Append("对手阻力矩  ").Append(torque.ToString("0.0")).Append(" N·m\n");
            builder.Append("数据更新    ").Append(dataAgeSeconds < 90f ? (dataAgeSeconds * 1000f).ToString("0") + " ms" : "—").Append('\n');
            builder.Append("设备状态    ").Append(DisplayNames.Of(deviceState)).Append('\n');
            builder.Append("数据源      ").Append(sourceName).Append('\n');
            builder.Append("链路        ").Append(DisplayNames.Of(linkStatus));
            if (!string.IsNullOrEmpty(linkDetail))
            {
                builder.Append('\n').Append("            ").Append(linkDetail);
            }

            return builder.ToString();
        }
    }
}


