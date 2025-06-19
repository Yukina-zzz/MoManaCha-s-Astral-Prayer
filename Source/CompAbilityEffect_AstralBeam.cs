using RimWorld;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    // 技能效果的实现
    public class CompAbilityEffect_AstralBeam : CompAbilityEffect
    {
        // 核心方法，当技能成功施放时调用
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = this.parent.pawn;

            // 确保有施法者和目标
            if (caster == null || !caster.Spawned)
            {
                return;
            }
            // 计算方向向量
            Vector3 direction = (target.Cell - caster.Position).ToVector3().normalized;

            // 获取技能的最大射程
            float maxRange = this.parent.def.verbProperties.range;

            // 计算光炮的理论终点 (从施法者位置沿方向延伸最大射程)
            IntVec3 endPoint = caster.Position + (direction * maxRange).ToIntVec3();

            //创建一个新的LocalTargetInfo作为光束的实际终点
            LocalTargetInfo beamTarget = new LocalTargetInfo(endPoint);

            // 生成我们的控制器Thing
            Thing_BeamController beamController = (Thing_BeamController)GenSpawn.Spawn(
                ThingDef.Named("MoManaCha_BeamController"),
                caster.Position,
                caster.Map
            );

            // 初始化控制器，传入所有需要的参数
            // parent.pawn.equipment.Primary 是获得此技能的武器
            beamController.Initialize(
                caster: caster,
                target: beamTarget,
                weapon: caster.equipment.Primary, // 从装备中获取武器
                damageDef: DefDatabase<DamageDef>.GetNamed("MoManaCha_AstralBeam"),
                totalBeamWidth: 3, // <--- 这里是修改的地方！
                damageAmount: 30,
                armorPenetration: 0.5f,
                duration: 5f,
                pulseInterval: 0.2f
            );
        }
    }
}