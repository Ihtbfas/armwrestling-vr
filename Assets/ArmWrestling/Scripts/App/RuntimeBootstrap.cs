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

        private void Awake()
        {
            if (FindObjectOfType<ArmWrestlingApp>() != null)
            {
                return;
            }

            ApplySkyRotationArgument();

            // Optional art is restored locally with its original GUIDs. Decide before
            // BuildArms so a public checkout keeps both capsule arms when art is absent.
            ArenaFactory.UseCharacterModel = useOriginalArt && Resources.Load<GameObject>("Characters/Ch03") != null;
            ArenaFactory.UseHdriSky = useOriginalArt && Resources.Load<Texture2D>("HDRI/circus_arena") != null;

            ArmWrestlingConfig config = ArmWrestlingConfig.Load();
            ArmWrestlingApp app = ArenaFactory.Build(config);
            if (useExperimentalGrip && ArenaFactory.UseCharacterModel)
            {
                var opponent = app.deviceRoot.GetComponentInChildren<OpponentAvatar>();
                if (opponent != null) opponent.useGripPose = true;
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
