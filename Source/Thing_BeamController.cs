using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    public class Thing_BeamController : Thing
    {
        // --- 参数 ---
        private Pawn caster;
        private LocalTargetInfo target;
        private Thing weapon;
        private DamageDef damageDef;
        private float beamWidth;
        private int damageAmount;
        private float armorPenetration;

        // --- 计时器 ---
        private int totalDurationTicks;
        private int pulseIntervalTicks;
        private int ticksCounter;
        private int ticksUntilNextPulse;

        // --- 路径和视觉 ---
        private List<IntVec3> fullBeamPathCells;
        private Mote_Beam beamMote;

        // 初始化方法，由Verb调用
        public void Initialize(Pawn caster, LocalTargetInfo target, Thing weapon, DamageDef damageDef, int beamWidth, int damageAmount, float armorPenetration, float duration, float pulseInterval)
        {
            this.caster = caster;
            this.target = target;
            this.weapon = weapon;
            this.damageDef = damageDef;
            this.beamWidth = beamWidth;
            this.damageAmount = damageAmount;
            this.armorPenetration = armorPenetration;

            this.totalDurationTicks = Mathf.RoundToInt(duration * 60f);
            this.pulseIntervalTicks = Mathf.RoundToInt(pulseInterval * 60f);
            this.ticksUntilNextPulse = 0; // 立即触发第一次

            // 在初始化时就计算好整个光束的几何路径
            this.fullBeamPathCells = CalculateBeamCells(caster.Position, target.Cell, beamWidth);
            if (this.Map != null)
            {
                beamMote = (Mote_Beam)ThingMaker.MakeThing(ThingDef.Named("MoManaCha_Mote_Beam"));
                beamMote.Initialize(caster.DrawPos, target.CenterVector3, beamWidth);
                GenSpawn.Spawn(beamMote, this.Position, this.Map);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void Tick()
        {
            if (caster == null || caster.Dead || !caster.Spawned || caster.Map != this.Map)
            {
                this.Destroy();
                return;
            }

            // 总时长控制
            if (ticksCounter >= totalDurationTicks)
            {
                this.Destroy();
                return;
            }

            if (beamMote != null)
            {
                beamMote.UpdateBeam(caster.DrawPos, target.CenterVector3);
            }

            // 脉冲间隔控制
            if (ticksUntilNextPulse <= 0)
            {
                DoBeamPulse();
                ticksUntilNextPulse = pulseIntervalTicks;
            }

            ticksCounter++;
            ticksUntilNextPulse--;
        }

        // 执行一次伤害脉冲
        private void DoBeamPulse()
        {
            if (fullBeamPathCells == null || fullBeamPathCells.Count == 0) return;

            GenExplosion.DoExplosion(
                center: this.caster.Position, // 爆炸中心永远是施法者
                map: this.Map,
                radius: 1f, // 不重要
                damType: this.damageDef,
                instigator: this.caster,
                damAmount: this.damageAmount,
                armorPenetration: this.armorPenetration,
                explosionSound: null, // 在Verb里已经播放了
                weapon: this.weapon.def,
                projectile: null,
                intendedTarget: this.target.Thing,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0.1f,
                damageFalloff: false, // 关闭伤害衰减
                ignoredThings: new List<Thing> { this.caster },
                overrideCells: this.fullBeamPathCells, // 使用我们计算的路径
                doVisualEffects: false, // 关闭默认爆炸特效
                doSoundEffects: false // 关闭默认爆炸音效
            );
        }

        // 计算光束覆盖的所有格子
        private List<IntVec3> CalculateBeamCells(IntVec3 start, IntVec3 end, float width)
        {
            var cells = new HashSet<IntVec3>();
            var linePoints = GenSight.PointsOnLineOfSight(start, end).ToList();
            if (linePoints.Count > 0) linePoints.RemoveAt(0); // 移除第一个点（施法者位置）
            Vector3 direction = (end - start).ToVector3().normalized;
            // 计算垂直向量
            Vector3 perpendicular = new Vector3(-direction.z, 0, direction.x);

            float halfWidth = (width - 1) / 2f;

            foreach (var point in linePoints)
            {
                foreach (var cell in GenRadial.RadialCellsAround(point, width / 2f, true))
                {
                    cells.Add(cell);
                }
                for (float i = 1; i <= halfWidth; i += 0.5f)
                {
                    // 使用Vector3进行计算
                    Vector3 p1_3d = point.ToVector3() + perpendicular * i;
                    Vector3 p2_3d = point.ToVector3() - perpendicular * i;
                    cells.Add(p1_3d.ToIntVec3());
                    cells.Add(p2_3d.ToIntVec3());
                }
            }

            return new List<IntVec3>(cells);
        }

        // 存档和读档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_TargetInfo.Look(ref target, "target");
            Scribe_References.Look(ref weapon, "weapon");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref beamWidth, "beamWidth");
            Scribe_Values.Look(ref damageAmount, "damageAmount");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref totalDurationTicks, "totalDurationTicks");
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks");
            Scribe_Values.Look(ref ticksCounter, "ticksCounter");
            Scribe_Values.Look(ref ticksUntilNextPulse, "ticksUntilNextPulse");
            Scribe_Collections.Look(ref fullBeamPathCells, "fullBeamPathCells", LookMode.Value);
            Scribe_References.Look(ref beamMote, "beamMote");
        }

        // 销毁时清理视觉效果
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (beamMote != null && !beamMote.Destroyed)
            {
                beamMote.Destroy();
            }
            base.Destroy(mode);
        }
    }
}