using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoManaCha_Astral_Prayer
{
    public class CompAbilityEffect_AstralBeam : CompAbilityEffect
    {
        public new CompProperties_AbilityAstralBeam Props => (CompProperties_AbilityAstralBeam)this.props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
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

            // 初始化控制器，所有参数都从 Props 读取
            beamController.Initialize(
                caster: caster,
                target: beamTarget,
                weapon: caster.equipment.Primary,
                damageDef: this.Props.damageDef, // 从Props读取
                totalBeamWidth: this.Props.beamWidth,
                damageAmount: this.Props.damageAmount,
                armorPenetration: this.Props.armorPenetration,
                duration: durationInSeconds,
                pulseInterval: this.Props.pulseInterval
            );

            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MoManaCha_Job_ChannelAstralBeam"), target, beamController);
            job.expiryInterval = (int)(this.Props.duration * 60f);
            caster.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}