using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 运行时场景构建入口。
    ///
    /// 场景中只有一个挂着本脚本的空物体时，进入 Play 会自动把整套扳手腕场景搭出来。
    /// 这样即使没有执行过编辑器里的“生成场景”菜单，工程也能直接运行——方便交接和排查。
    /// 如果场景里已经烘焙好完整内容（存在 ArmWrestlingApp），本脚本不会重复创建。
    /// </summary>
    public class RuntimeBootstrap : MonoBehaviour
    {
        [Tooltip("勾选后启动时打印一次场景构建信息。")]
        public bool verbose = true;

        [Tooltip("仅本地恢复原始人物/HDRI 后使用。原人物姿态和材质仍需调整；默认展示胶囊场景。")]
        public bool useOriginalArt;
        [Tooltip("启用 Ch03 右手握姿预研。需要先勾选 Use Original Art。")]
        public bool useExperimentalGrip;
        [Tooltip("启用本地人体玩家手臂、近固定对手肘及骨骼片段。需要恢复 Ch03 并勾选 Use Original Art。")]
        public bool useCharacterMotion;
        [Tooltip("在人物动画模式中启用双方握持姿态和玩家衣袖。")]
        public bool useHandContact;
        [Tooltip("Use the Rocketbox avatar and the authored paired grip.")]
        public bool usePairedGrip;
        [Tooltip("P2-B。仅用于 Rocketbox 配对握姿。可只诊断，或在诊断基础上修正。")]
        public PairedCollisionMode pairedCollisions;
        public bool showCollisionGizmos;
        public bool useHumanoidGrip;
        public HumanoidGripDefinition humanoidGrip;
        public GripAvatarProfile playerAvatar, opponentAvatar;
        private PairedGripPose resolvedGrip;

        private void OnDestroy()
        {
            if (resolvedGrip != null) Destroy(resolvedGrip);
        }

        private void Awake()
        {
            if (FindObjectOfType<ArmWrestlingApp>() != null)
            {
                return;
            }

            ApplySkyRotationArgument();

            // Optional art is restored locally with its original GUIDs. Decide before
            // BuildArms so a public checkout keeps both capsule arms when art is absent.
            var effectiveCollisions = pairedCollisions;
            ArenaFactory.PairedProfile = null;
            var pair = usePairedGrip ? Resources.Load<PairedGripPose>(PairedGripPose.PoseResource) : null;
            if (pair != null && pairedCollisions == PairedCollisionMode.ContactGrip)
            {
                var contactPose = Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource);
                if (contactPose != null && contactPose.HasContactEndpoints) pair = contactPose;
                else
                {
                    effectiveCollisions = PairedCollisionMode.LegacyContactGrip;
                    Debug.LogWarning("[P2-C] Authored contact pose is missing or incomplete. Using the legacy paired grip and solver.");
                }
            }
            if (useHumanoidGrip)
            {
                try
                {
                    resolvedGrip = HumanoidGripBuilder.Build(humanoidGrip, playerAvatar, opponentAvatar, out _, out _);
                    pair = resolvedGrip;
                    ArenaFactory.PairedProfile = opponentAvatar;
                    effectiveCollisions = PairedCollisionMode.ContactGrip;
                }
                catch (System.Exception error)
                {
                    Debug.LogWarning("[P2-D] Humanoid profile could not bind; using the selected P2-C/capsule fallback. " + error.Message);
                }
            }
            var rocketbox = pair != null && pair.IsComplete
                ? (ArenaFactory.PairedProfile != null ? opponentAvatar.prefab : Resources.Load<GameObject>(PairedGripPose.AvatarResource)) : null;
            var rocketAnimator = rocketbox != null ? rocketbox.GetComponent<Animator>() : null;
            ArenaFactory.PairedAvatar = rocketAnimator != null && rocketAnimator.isHuman && rocketAnimator.avatar.isValid
                ? rocketbox : null;
            if (usePairedGrip && ArenaFactory.PairedAvatar == null)
                Debug.LogWarning("[P2-A] The Rocketbox avatar or paired pose is incomplete. Using the selected fallback.");
            ArenaFactory.UseCharacterModel = ArenaFactory.PairedAvatar != null
                || (useOriginalArt && Resources.Load<GameObject>("Characters/Ch03") != null);
            ArenaFactory.UseHdriSky = useOriginalArt && Resources.Load<Texture2D>("HDRI/circus_arena") != null;

            ArmWrestlingConfig config = ArmWrestlingConfig.Load();
            ArmWrestlingApp app = ArenaFactory.Build(config);
            if (ArenaFactory.PairedAvatar != null)
            {
                var opponent = app.deviceRoot.GetComponentInChildren<OpponentAvatar>();
                app.armRig.playerVisual = PlayerArmVisual.TryCreatePaired(app.armRig,
                    ArenaFactory.PairedProfile != null ? playerAvatar.prefab : ArenaFactory.PairedAvatar, pair.player,
                    ArenaFactory.PairedProfile != null ? playerAvatar.modelScale : PairedGripPose.AvatarScale,
                    ArenaFactory.PairedProfile != null ? playerAvatar : null);
                if (opponent != null && app.armRig.playerVisual != null)
                {
                    opponent.EnableCharacterMotion(app, false);
                    opponent.SetPairedGrip(pair.opponent);
                    if (ArenaFactory.PairedProfile != null) opponent.SetGripProfile(opponentAvatar);
                    if (effectiveCollisions != PairedCollisionMode.Off)
                        opponent.EnableCollisionExperiment(app.armRig, effectiveCollisions, showCollisionGizmos, resolvedGrip);
                }
                app.armRig.ApplyAngle(0f);
            }
            else if ((useExperimentalGrip || useCharacterMotion) && ArenaFactory.UseCharacterModel)
            {
                var opponent = app.deviceRoot.GetComponentInChildren<OpponentAvatar>();
                if (opponent != null && useExperimentalGrip) opponent.useGripPose = true;
                if (useCharacterMotion)
                {
                    app.armRig.playerVisual = PlayerArmVisual.TryCreate(app.armRig, Resources.Load<GameObject>("Characters/Ch03"));
                    if (opponent != null && app.armRig.playerVisual != null)
                    {
                        opponent.EnableCharacterMotion(app, useHandContact);
                        if (useHandContact) app.armRig.playerVisual.EnableHandContact();
                    }
                    app.armRig.ApplyAngle(0f);
                }
            }

            if (verbose)
            {
                Debug.Log("[ArmWrestling] 已在运行时构建场景：" + app.name + "（数据源 " + config.defaultDataSource + "）");
            }
        }

        /// <summary>命令行 -skyrot 200 可以现场调整天空盒朝向，不用重新打包。</summary>
        private static void ApplySkyRotationArgument()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-skyrot")
                {
                    continue;
                }

                float value;
                if (float.TryParse(args[i + 1], out value))
                {
                    ArenaFactory.SkyRotation = value;
                    Debug.Log("[ArmWrestling] 天空盒朝向 = " + value);
                }
            }
        }
    }
}
