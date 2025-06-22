using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MoManaCha_Astral_Prayer
{
    /// <summary>
    /// 控制Pawn在引导“星穹光炮”技能期间的行为。
    /// 主要职责是：锁定Pawn位置、维持正确的朝向和持续的瞄准动画。
    /// </summary>
    public class JobDriver_ChannelAstralBeam : JobDriver
    {
        // 通过属性方便地获取在Job创建时传入的目标B（光束控制器）
        private Thing_BeamController BeamController => TargetB.Thing as Thing_BeamController;

        /// <summary>
        /// 声明此Job不需要预定任何地图上的对象（如床、工作台），因此直接返回true。
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        /// <summary>
        /// 构建此Job要执行的一系列行为（Toils）。
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 创建一个Toil，它将代表整个引导过程。
            Toil channelToil = new Toil();

            // --- 1. 清理逻辑 (FinishAction) ---
            // 定义Job在任何情况下结束（完成、被中断、失败）时都必须执行的清理工作。
            channelToil.AddFinishAction(() =>
            {
                // 确保光束的视觉和伤害效果与Job同步停止。
                BeamController?.Stop();

                // 将Pawn的姿态恢复为正常的可移动状态，这是避免角色卡在瞄准姿势的关键。
                if (pawn.stances?.curStance is Stance_Warmup)
                {
                    pawn.stances.SetStance(new Stance_Mobile());
                }
            });

            // --- 2. 初始化逻辑 (initAction) ---
            // 定义Toil开始时仅执行一次的动作。
            channelToil.initAction = () =>
            {
                // 锁定Pawn的位置，使其无法移动。
                pawn.pather.StopDead();

                // 将Pawn的姿态设置为Stance_Warmup，这是触发瞄准动画和视觉效果的关键。
                // 传入武器的Verb可以帮助游戏正确地渲染持握武器的姿势。
                // 持续时间设为-1（无限），因为Toil的生命周期由其自身的defaultDuration控制。
                var primaryVerb = pawn.equipment?.Primary?.GetComp<CompEquippable>()?.PrimaryVerb;
                pawn.stances.SetStance(new Stance_Warmup(-1, TargetA, primaryVerb));
            };

            // --- 3. 每帧更新逻辑 (tickAction) ---
            // 定义在Toil持续期间每一帧都要执行的动作。
            channelToil.tickAction = () =>
            {
                // 此处唯一的逻辑是作为一个“保险”，防止其他Mod或游戏逻辑意外重置了我们的姿态。
                // 如果检测到姿态被改变，就立刻把它设置回来，确保引导动画不中断。
                if (!(pawn.stances.curStance is Stance_Warmup))
                {
                    var primaryVerb = pawn.equipment?.Primary?.GetComp<CompEquippable>()?.PrimaryVerb;
                    pawn.stances.SetStance(new Stance_Warmup(-1, TargetA, primaryVerb));
                }
            };

            // --- 4. 结束条件 ---
            // 设置Toil的完成模式为“延迟”，即持续一段固定的时间。
            channelToil.defaultCompleteMode = ToilCompleteMode.Delay;
            // 将Toil的持续时间与Job的过期时间同步，该时间在技能效果（CompAbilityEffect）中设置。
            channelToil.defaultDuration = this.job.expiryInterval;

            yield return channelToil;
        }

        /// <summary>
        /// 允许玩家通过征召、下达新指令等方式手动打断这个Job。
        /// </summary>
        public override bool PlayerInterruptable => true;

    }
}