using RimWorld;
using System.Collections.Generic; // 引入此命名空间
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoManaCha_Astral_Prayer
{
    public class CompAbilityEffect_AstralBeam : CompAbilityEffect
    {
        public new CompProperties_AbilityAstralBeam Props => (CompProperties_AbilityAstralBeam)this.props;

        // 【新增】重写 Valid 方法
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            // 调用工具类检查是否处于过载状态
            bool isOverload = AstralBeamUtility.IsOverloadCondition(
                this.parent.pawn,
                target,
                this.parent.pawn.Map,
                this.Props.beamWidth,
                this.parent.def.verbProperties.range,
                this.Props.minSafeDistance
            );

            if (isOverload)
            {
                // 如果是过载状态，且需要显示消息
                if (throwMessages)
                {
                    Messages.Message("目标过近，会导致能量过载！", target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false; // 返回 false 表示目标无效
            }

            // 如果不过载，则继续执行基类的检查
            return base.Valid(target, throwMessages);
        }


        // 【已修改】重写 DrawEffectPreview 方法
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            // 获取受影响的格子
            List<IntVec3> affectedCells = AstralBeamUtility.GetAffectedCells(
                this.parent.pawn.DrawPos,
                target,
                this.parent.pawn.Map,
                this.Props.beamWidth,
                this.parent.def.verbProperties.range,
                this.parent.pawn
            );

            // 【核心修改】根据 Valid() 的结果选择颜色
            Color drawColor = this.Valid(target) ? Color.white : Color.red;

            GenDraw.DrawFieldEdges(affectedCells, drawColor);
        }


        // Apply 方法和 JobDefOf 静态类保持不变
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // ... (Apply 方法内容不变) ...
            base.Apply(target, dest);

            Pawn caster = this.parent.pawn;
            if (caster == null || !caster.Spawned) return;

            float maxRange = this.parent.def.verbProperties.range;
            float durationInSeconds = this.Props.duration;

            Vector3 direction = (target.Cell - caster.Position).ToVector3().normalized;
            IntVec3 endPoint = caster.Position + (direction * maxRange).ToIntVec3();
            LocalTargetInfo beamTarget = new LocalTargetInfo(endPoint);

            Thing_BeamController beamController = (Thing_BeamController)GenSpawn.Spawn(
                ThingDef.Named("MoManaCha_BeamController"),
                caster.Position,
                caster.Map
            );

            beamController.Initialize(
                caster: caster,
                target: beamTarget,
                weapon: caster.equipment.Primary,
                damageDef: this.Props.damageDef,
                totalBeamWidth: this.Props.beamWidth,
                damageAmount: this.Props.damageAmount,
                armorPenetration: this.Props.armorPenetration,
                duration: durationInSeconds,
                pulseInterval: this.Props.pulseInterval,
                maxRange: maxRange
            );

            Job job = JobMaker.MakeJob(JobDefOf.MoManaCha_Job_ChannelAstralBeam, target, beamController);
            job.expiryInterval = Mathf.RoundToInt(durationInSeconds * 60f) + 10;
            caster.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        [DefOf]
        public static class JobDefOf
        {
            public static JobDef MoManaCha_Job_ChannelAstralBeam;

            static JobDefOf()
            {
                DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf));
            }
        }
    }
}