using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    public class Thing_BeamController : Thing
    {
        // --- 参数和计时器 ---
        private Pawn caster;
        private LocalTargetInfo target;
        private Thing weapon;
        private DamageDef damageDef;
        private float beamWidth;
        private int damageAmount;
        private float armorPenetration;
        private int totalDurationTicks;
        private int pulseIntervalTicks;
        private int ticksCounter;
        private int ticksUntilNextPulse;

        // --- 路径和视觉 ---
        private List<IntVec3> geometricPath; // 几何中心线路径
        private List<IntVec3> effectiveCells; // 实际伤害格子
        private Vector3 visualEndPointVec; // 视觉终点
        private Mote_Beam beamMote;

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
            this.ticksUntilNextPulse = 0;
            this.effectiveCells = new List<IntVec3>();

            this.geometricPath = CalculateGeometricPath(caster.Position, target.Cell);
            
            CalculateEffectiveRange(); 

            if (this.Map != null)
            {
                beamMote = (Mote_Beam)ThingMaker.MakeThing(ThingDef.Named("MoManaCha_Mote_Beam"));
                beamMote.Initialize(caster.DrawPos, this.visualEndPointVec, beamWidth);
                GenSpawn.Spawn(beamMote, caster.Position, this.Map);
            }
        }

        public override void Tick()
        {
            if (caster == null || caster.Dead || !caster.Spawned || caster.Map != this.Map || (beamMote != null && beamMote.Destroyed))
            {
                this.Destroy();
                return;
            }
            if (ticksCounter >= totalDurationTicks)
            {
                this.Destroy();
                return;
            }

            if (beamMote != null)
            {
                beamMote.UpdateBeam(caster.DrawPos, visualEndPointVec);
            }

            if (ticksUntilNextPulse <= 0)
            {
                CalculateEffectiveRange();

                // 去除重复的格子，小幅优化性能
                var uniqueCells = new HashSet<IntVec3>(effectiveCells).ToList();

                GenExplosion.DoExplosion(
                    center: caster.Position,
                    map: this.Map,
                    radius: 0f,
                    damType: this.damageDef,
                    instigator: caster,
                    damAmount: this.damageAmount,
                    armorPenetration: this.armorPenetration,
                    weapon: this.weapon.def,
                    ignoredThings: new List<Thing> { caster },
                    overrideCells: uniqueCells,
                    damageFalloff: false,
                    doVisualEffects: false,
                    doSoundEffects: false
                );

                ticksUntilNextPulse = pulseIntervalTicks;
            }

            ticksCounter++;
            ticksUntilNextPulse--;
        }

        // 计算并更新实际伤害范围和视觉终点
        private void CalculateEffectiveRange()
        {
            effectiveCells.Clear();

            Vector3 startVec = caster.Position.ToVector3Shifted();
            Vector3 targetVec = target.CenterVector3;
            Vector3 direction = (targetVec - startVec).normalized;

            // 默认视觉终点就是起点，如果路径为空，这就是最终结果
            visualEndPointVec = startVec;

            IntVec3 lastEffectiveCenterPoint = caster.Position;

            foreach (IntVec3 centerPoint in geometricPath)
            {
                if (centerPoint == caster.Position) continue;

                Building edifice = centerPoint.GetEdifice(this.Map);
                if (edifice != null && edifice.def.Fillage == FillCategory.Full)
                {
                    // --- 发现墙体 ---
                    // 将墙体切片中可见的部分加入伤害列表
                    List<IntVec3> finalSlice = new List<IntVec3>();
                    float sliceRadius = (beamWidth - 1) / 2f;
                    foreach (var cell in CalculateSliceCells(centerPoint, sliceRadius))
                    {
                        if (GenSight.LineOfSight(caster.Position, cell, this.Map, skipFirstCell: true))
                        {
                            finalSlice.Add(cell);
                        }
                    }
                    effectiveCells.AddRange(finalSlice);

                    // 记录最后一个有效中心点就是这个墙体所在点
                    lastEffectiveCenterPoint = centerPoint;
                    break; // 找到墙就中断循环
                }
                else
                {
                    // --- 路径通畅 ---
                    float sliceRadius = (beamWidth - 1) / 2f;
                    effectiveCells.AddRange(CalculateSliceCells(centerPoint, sliceRadius));
                    // 持续更新最后一个有效中心点
                    lastEffectiveCenterPoint = centerPoint;
                }
            }

            // --- 统一计算最终的视觉终点 ---
            if (lastEffectiveCenterPoint != caster.Position)
            {
                // 计算从起点到最后一个有效中心点格子的距离
                float distanceToEndPoint = Vector3.Distance(startVec, lastEffectiveCenterPoint.ToVector3Shifted());

                // 将这个距离减去0.5（半个格子的标准距离），让视觉光束的末端停在最后一个格子的近侧边缘
                float visualLength = Mathf.Max(0, distanceToEndPoint - 0.5f);

                // 根据计算出的视觉长度，确定最终的视觉终点浮点坐标
                visualEndPointVec = startVec + direction * visualLength;
            }
        }

        // 只计算中心线几何路径
        private List<IntVec3> CalculateGeometricPath(IntVec3 start, IntVec3 end)
        {
            return GenSight.PointsOnLineOfSight(start, end).ToList();
        }

        private List<IntVec3> CalculateSliceCells(IntVec3 center, float radius)
        {
            return GenRadial.RadialCellsAround(center, radius, true).ToList();
        }


        // ExposeData 和 Destroy 方法保持不变
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
            Scribe_Collections.Look(ref geometricPath, "geometricPath", LookMode.Value);
            Scribe_References.Look(ref beamMote, "beamMote");
            Scribe_Values.Look(ref visualEndPointVec, "visualEndPointVec");
        }

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