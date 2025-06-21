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
        private int totalBeamWidth;
        private int damageAmount;
        private float armorPenetration;
        private int totalDurationTicks;
        private int pulseIntervalTicks;
        private int ticksCounter;
        private int ticksUntilNextPulse;

        // --- 路径和视觉 ---
        private List<List<IntVec3>> subPaths;
        private List<Mote_Beam> beamMotes;
        private List<Vector3> subBeamStartOffsets;
        private List<IntVec3> effectiveCells;

        // <<-- NEW HELPER METHOD -->>
        // 创建一个新的私有方法，专门用于初始化/重置列表状态。
        // 这样我们就可以在任何需要的时候安全地调用它，而不会触及Thing的ID分配。
        private void ResetState()
        {
            subPaths = new List<List<IntVec3>>();
            beamMotes = new List<Mote_Beam>();
            subBeamStartOffsets = new List<Vector3>();
            effectiveCells = new List<IntVec3>();
        }

        public override void PostMake()
        {
            base.PostMake();
            // PostMake现在只做它应该做的事：在Thing首次创建时调用ResetState。
            ResetState();
        }

        public void Initialize(Pawn caster, LocalTargetInfo target, Thing weapon, DamageDef damageDef, int totalBeamWidth, int damageAmount, float armorPenetration, float duration, float pulseInterval)
        {
            this.caster = caster;
            this.target = target;
            this.weapon = weapon;
            this.damageDef = damageDef;
            this.totalBeamWidth = totalBeamWidth;
            this.damageAmount = damageAmount;
            this.armorPenetration = armorPenetration;

            this.totalDurationTicks = Mathf.RoundToInt(duration * 60f);
            this.pulseIntervalTicks = Mathf.RoundToInt(pulseInterval * 60f);
            this.ticksUntilNextPulse = 0;

            // <<-- CRITICAL FIX -->>
            // 移除了错误的 PostMake() 调用。
            // PostMake(); // <<-- OLD INCORRECT CALL REMOVED

            // 使用我们新的、安全的方法来重置列表。
            ResetState(); // <<-- NEW CORRECT CALL

            if (caster != null && this.Map != null)
            {
                GenerateSubBeams();
            }
        }

        private void GenerateSubBeams()
        {
            // 清理可能存在的旧Mote，以防万一（比如在加载后重新初始化时）
            if (beamMotes != null)
            {
                foreach (var oldMote in beamMotes.Where(m => m != null && !m.Destroyed))
                {
                    oldMote.Destroy();
                }
            }
            // 重置列表确保我们从一个干净的状态开始
            ResetState();

            Vector3 startVec = caster.Position.ToVector3Shifted();
            Vector3 endVec = target.CenterVector3;
            Vector3 direction = (endVec - startVec).normalized;
            Vector3 perpendicularDir = new Vector3(direction.z, 0, -direction.x).normalized;

            float subBeamWidth = 1.1f;
            float initialOffset = -(totalBeamWidth - 1) / 2.0f;

            for (int i = 0; i < totalBeamWidth; i++)
            {
                float offsetMagnitude = initialOffset + i;
                Vector3 currentOffsetVector = perpendicularDir * offsetMagnitude;
                subBeamStartOffsets.Add(currentOffsetVector);

                Vector3 subStartVec = startVec + currentOffsetVector;
                Vector3 subEndVec = endVec + currentOffsetVector;

                List<IntVec3> path = CalculateGeometricPath(subStartVec.ToIntVec3(), subEndVec.ToIntVec3());
                subPaths.Add(path);

                Mote_Beam mote = (Mote_Beam)ThingMaker.MakeThing(ThingDef.Named("MoManaCha_Mote_Beam"));
                mote.Initialize(subStartVec, subEndVec, subBeamWidth);
                GenSpawn.Spawn(mote, caster.Position, this.Map);
                beamMotes.Add(mote);
            }
        }

        // Tick() 及其他方法保持不变...
        public override void Tick()
        {
            if (caster == null || caster.Dead || !caster.Spawned || caster.Map != this.Map)
            {
                this.Destroy();
                return;
            }

            if (beamMotes.Any(m => m == null || m.Destroyed))
            {
                this.Destroy();
                return;
            }

            if (ticksCounter >= totalDurationTicks)
            {
                this.Destroy();
                return;
            }

            effectiveCells.Clear();
            Vector3 currentCasterDrawPos = caster.DrawPos;
            Vector3 targetVec = target.CenterVector3;
            Vector3 mainDirection = (targetVec - currentCasterDrawPos).normalized;

            for (int i = 0; i < subPaths.Count; i++)
            {
                Vector3 subStartVec = currentCasterDrawPos + subBeamStartOffsets[i];

                CalculateSubBeamCollision(
                    path: subPaths[i],
                    startVec: subStartVec,
                    direction: mainDirection,
                    casterPosition: caster.Position,
                    out List<IntVec3> pathEffectiveCells,
                    out Vector3 visualEndPoint
                );

                effectiveCells.AddRange(pathEffectiveCells);

                if (i < beamMotes.Count && beamMotes[i] != null && !beamMotes[i].Destroyed)
                {
                    beamMotes[i].UpdateBeam(subStartVec, visualEndPoint);
                }
            }

            if (ticksUntilNextPulse <= 0)
            {
                var uniqueCells = new HashSet<IntVec3>(effectiveCells).ToList();

                if (uniqueCells.Count > 0)
                {
                    GenExplosion.DoExplosion(
                        center: caster.Position,
                        map: this.Map,
                        radius: 0f,
                        damType: this.damageDef,
                        instigator: caster,
                        damAmount: this.damageAmount,
                        armorPenetration: this.armorPenetration,
                        weapon: this.weapon?.def,
                        ignoredThings: new List<Thing> { caster },
                        overrideCells: uniqueCells,
                        damageFalloff: false,
                        doVisualEffects: false,
                        doSoundEffects: false
                    );
                }

                ticksUntilNextPulse = pulseIntervalTicks;
            }

            ticksCounter++;
            ticksUntilNextPulse--;
        }

        private void CalculateSubBeamCollision(List<IntVec3> path, Vector3 startVec, Vector3 direction, IntVec3 casterPosition, out List<IntVec3> pathEffectiveCells, out Vector3 visualEndPoint)
        {
            pathEffectiveCells = new List<IntVec3>();
            visualEndPoint = startVec;

            IntVec3 lastEffectivePoint = casterPosition;
            bool obstacleHit = false; // 判断是否撞到了障碍物

            foreach (IntVec3 point in path)
            {
                if (point == casterPosition) continue;

                Building edifice = point.GetEdifice(this.Map);
                if (edifice != null && edifice.def.Fillage == FillCategory.Full)
                {
                    if (point != casterPosition)
                    {
                        pathEffectiveCells.Add(point);
                    }

                    lastEffectivePoint = point;
                    obstacleHit = true; // 标记我们撞到了障碍物
                    break; // 找到障碍物，光束终止
                }
                else
                {
                    pathEffectiveCells.Add(point);
                    lastEffectivePoint = point;
                }
            }

            // 如果光束是飞到最大射程而没有撞到任何东西
            if (!obstacleHit)
            {
                // 保持现有逻辑，让光束延伸到最后一个有效格子的中心或边缘
                float distanceToEndPoint = Vector3.Distance(startVec, lastEffectivePoint.ToVector3Shifted());
                // 可以稍微延伸出一点点，让视觉效果更饱满
                float visualLength = Mathf.Max(0, distanceToEndPoint + 0.5f);
                visualEndPoint = startVec + direction * visualLength;
            }
            else // 如果光束撞到了障碍物 (lastEffectivePoint 就是那个障碍物格子)
            {
                // 创建一个代表障碍物格子边界的包围盒 (Bounds)
                // 这个包围盒的中心是格子的中心，大小是1x1x1
                var obstacleBounds = new Bounds(lastEffectivePoint.ToVector3Shifted(), Vector3.one);

                // 创建一条从光束起点出发的射线
                var ray = new Ray(startVec, direction);

                // 使用 Bounds.IntersectRay 方法计算射线与包围盒的交点
                // 这个方法会返回相交的距离
                if (obstacleBounds.IntersectRay(ray, out float intersectionDistance))
                {
                    // 计算精确的交点坐标
                    visualEndPoint = ray.GetPoint(intersectionDistance);
                }
                else
                {
                    // 作为备用方案，如果射线投射失败（理论上不应该），
                    // 就退回到之前的方法，停在格子中心附近
                    float distanceToEndPoint = Vector3.Distance(startVec, lastEffectivePoint.ToVector3Shifted());
                    float visualLength = Mathf.Max(0, distanceToEndPoint - 0.5f);
                    visualEndPoint = startVec + direction * visualLength;
                }
            }

        }

        private List<IntVec3> CalculateGeometricPath(IntVec3 start, IntVec3 end)
        {
            return GenSight.PointsOnLineOfSight(start, end).ToList();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_TargetInfo.Look(ref target, "target");
            Scribe_References.Look(ref weapon, "weapon");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref totalBeamWidth, "totalBeamWidth");
            Scribe_Values.Look(ref damageAmount, "damageAmount");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref totalDurationTicks, "totalDurationTicks");
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks");
            Scribe_Values.Look(ref ticksCounter, "ticksCounter");
            Scribe_Values.Look(ref ticksUntilNextPulse, "ticksUntilNextPulse");

            Scribe_Collections.Look(ref beamMotes, "beamMotes", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (caster != null)
                {
                    // 在加载后，我们需要重新生成那些非持久化的数据，比如路径。
                    // Initialize方法现在可以安全地被调用来完成这个工作。
                    // 注意：这里我们传入已保存的参数来重建状态。
                    Initialize(caster, target, weapon, damageDef, totalBeamWidth, damageAmount, armorPenetration, (float)totalDurationTicks / 60f, (float)pulseIntervalTicks / 60f);
                }
                else
                {
                    this.Destroy();
                }
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (beamMotes != null)
            {
                foreach (var mote in beamMotes)
                {
                    if (mote != null && !mote.Destroyed)
                    {
                        mote.Destroy();
                    }
                }
            }
            base.Destroy(mode);
        }

        public void Stop()
        {
            // 调用Destroy方法，它会处理Mote的销毁等清理工作
            if (!this.Destroyed)
            {
                this.Destroy();
            }
        }

    }
}