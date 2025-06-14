using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MoManaCha_Astral_Prayer
{
    /// <summary>
    /// 一个简单的数据容器类，用于在XML中定义一个武器形态。
    /// 它不是一个Def，只是一个普通的C#类，方便XML解析器加载数据。
    /// </summary>
    public class ModeDef
    {
        public string label;
        public string description;
        public VerbProperties verbProps;
    }

    /// <summary>
    /// 多形态武器组件本身，负责处理状态切换和UI按钮（Gizmo）。
    /// </summary>
    public class CompMultiModeWeapon : ThingComp
    {
        public CompProperties_MultiMode Props => (CompProperties_MultiMode)this.props;

        private int currentIndex = 0;

        // 缓存主攻击动作，避免每次都获取，提高性能
        //private Verb primaryVerb;

        // 缓存我们的UI图标
        [Unsaved] // 不需要保存到存档中
        private Texture2D switchModeIcon;

        public ModeDef CurrentMode => Props.modes[currentIndex];

        // 为了方便，获取持有武器的小人
        private Pawn CasterPawn
        {
            get
            {
                if (this.parent.ParentHolder is Pawn_EquipmentTracker tracker)
                {
                    return tracker.pawn;
                }
                return null;
            }
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            SyncVerbProps();
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            SyncVerbProps();
        }

        /// <summary>
        /// 在组件初始化或从存档加载后，确保Verb与当前模式同步。
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentIndex, "currentIndex", 0);

            // 当游戏加载完毕后，我们可能需要强制同步一次Verb属性
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SyncVerbProps();
            }
        }

        /// <summary>
        /// 当物品生成在地图上或被装备时调用。
        /// </summary>
        //public override void PostSpawnSetup(bool respawningAfterLoad)
        //{
        //    base.PostSpawnSetup(respawningAfterLoad);
        //    // 确保我们拿到了图标
        //    LongEventHandler.ExecuteWhenFinished(() =>
        //    {
        //        if (!Props.uiIconPath.NullOrEmpty())
        //        {
        //            switchModeIcon = ContentFinder<Texture2D>.Get(Props.uiIconPath);
        //        }
        //    });
        //    // 立即同步一次
        //    SyncVerbProps();
        //}


        /// <summary>
        /// 切换到下一个模式
        /// </summary>
        public void SwitchMode()
        {
            currentIndex++;
            if (currentIndex >= Props.modes.Count)
            {
                currentIndex = 0;
            }

            // 【核心】切换模式后，立即同步Verb的属性
            SyncVerbProps();

            // 如果武器正被持有，给玩家反馈
            if (CasterPawn != null && CasterPawn.IsColonistPlayerControlled)
            {
                SoundDefOf.Click.PlayOneShotOnCamera(null);
                Messages.Message($"星穹祈愿诗: 已切换至 [{CurrentMode.label}] 形态。", MessageTypeDefOf.SilentInput);
            }
        }

        /// <summary>
        /// 将当前模式的属性应用到武器的主Verb上。
        /// </summary>
        private void SyncVerbProps()
        {
            var compEquippable = parent.GetComp<CompEquippable>();
            if (compEquippable != null)
            {
                Verb primaryVerb = compEquippable.PrimaryVerb;
                if (primaryVerb != null && Props.modes != null && Props.modes.Count > currentIndex)
                {
                    // 用当前模式的verbProps替换掉武器主攻击的verbProps
                    primaryVerb.verbProps = CurrentMode.verbProps;
                    // 切换后可能需要重置verb的状态，以防它卡在例如“瞄准”等状态
                    primaryVerb.Reset();
                }
            }
        }

        public IEnumerable<Gizmo> GetEquippedGizmos()
        {
            // 只有当持有者是玩家可控小人时，我们才显示这个按钮
            if (CasterPawn != null && CasterPawn.IsColonistPlayerControlled && Props.modes.Count > 1)
            {
                if (this.switchModeIcon == null && !Props.uiIconPath.NullOrEmpty())
                {
                    this.switchModeIcon = ContentFinder<Texture2D>.Get(Props.uiIconPath);
                }

                int nextIndex = (currentIndex + 1) % Props.modes.Count;
                ModeDef nextMode = Props.modes[nextIndex];

                var command = new Command_Action
                {
                    icon = this.switchModeIcon,
                    defaultLabel = "切换形态",
                    defaultDesc = $"切换至: {nextMode.label}\n({nextMode.description})\n\n当前: {CurrentMode.label}\n({CurrentMode.description})",
                    action = () => SwitchMode()
                };
                yield return command;
            }
        }

    }

}