using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    [StaticConstructorOnStartup]
    public class Thing_BeamController : Thing
    {
        // --- 参数 & 计时器 (无变化) ---
        private Pawn caster;
        private LocalTargetInfo target;
        private Thing weapon;
        private DamageDef damageDef;
        private int totalBeamWidth;
        private int damageAmount;
        private float armorPenetration;
        private int totalDurationTicks;
        private int pulseIntervalTicks;
        private float maxRange;
        private int ticksCounter;
        private int ticksUntilNextPulse;

        // --- 视觉分段管理 (无变化) ---
        private Mote_Beam headMote;
        private readonly List<Mote_Beam> middleMotes = new List<Mote_Beam>();
        private readonly List<Mote_Beam> tailMotes = new List<Mote_Beam>();

        // --- 静态 Def 引用 (无变化) ---
        private static readonly ThingDef Mote_Head = ThingDef.Named("Mote_AstralBeam_Head");
        private static readonly ThingDef Mote_Middle = ThingDef.Named("Mote_AstralBeam_Middle");
        private static readonly ThingDef Mote_Tail = ThingDef.Named("Mote_AstralBeam_Tail");

        // --- 逻辑常量 (无变化) ---
        private const float SegmentLengthSimilarityThreshold = 1.0f;
        private const float MinSafeCasterDistance = 2.0f; // 【新】定义最小安全施法距离

        // --- 构造/初始化/控制方法 (无变化) ---
        public void Initialize(Pawn caster, LocalTargetInfo target, Thing weapon, DamageDef damageDef, int totalBeamWidth, int damageAmount, float armorPenetration, float duration, float pulseInterval, float maxRange)
        {
            this.caster = caster;
            this.target = target;
            this.weapon = weapon;
            this.damageDef = damageDef;
            this.totalBeamWidth = totalBeamWidth;
            this.damageAmount = damageAmount;
            this.armorPenetration = armorPenetration;
            this.maxRange = maxRange;

            this.totalDurationTicks = Mathf.RoundToInt(duration * 60f);
            this.pulseIntervalTicks = Mathf.RoundToInt(pulseInterval * 60f);
        }

        public void Stop()
        {
            if (!this.Destroyed) this.Destroy();
        }

        // --- Tick & 伤害逻辑 (无变化) ---
        public override void Tick()
        {
            if (caster == null || caster.Dead || !caster.Spawned || caster.Map != this.Map || ticksCounter >= totalDurationTicks)
            {
                this.Destroy();
                return;
            }

            // 2. --- 【新】过载爆炸安全检查 ---
            if (CheckForOverloadCondition())
            {
                // 触发爆炸并中断技能
                GenExplosion.DoExplosion(
                    center: caster.Position,
                    map: this.Map,
                    radius: 2f,
                    damType: this.damageDef,
                    instigator: caster,
                    damAmount: this.damageAmount,
                    armorPenetration: this.armorPenetration,
                    weapon: this.weapon?.def,
                    explosionSound: SoundDef.Named("Explosion_Bomb"),
                    ignoredThings: new List<Thing> { caster }, // 确保不会伤害施法者
                    doVisualEffects: true,
                    doSoundEffects: true,
                    screenShakeFactor: 0.5f
                );
                this.Destroy();
                return;
            }


            UpdateBeamVisuals();

            if (ticksUntilNextPulse <= 0)
            {
                ApplyDamage();
                ticksUntilNextPulse = pulseIntervalTicks;
            }

            ticksCounter++;
            ticksUntilNextPulse--;
        }

        // 检查是否满足过载爆炸的条件
        private bool CheckForOverloadCondition()
        {
            // 直接调用新的公共工具方法
            return AstralBeamUtility.IsOverloadCondition(
                this.caster,
                this.target,
                this.Map,
                this.totalBeamWidth,
                this.maxRange,
                2.0f
            );
        }

        private void UpdateBeamVisuals()
        {
            // 1. --- 初始设置与清理 ---
            CleanUpMotes(middleMotes);
            CleanUpMotes(tailMotes);

            Vector2 casterPos2D = new Vector2(caster.DrawPos.x, caster.DrawPos.z);
            Vector2 targetPos2D = new Vector2(target.CenterVector3.x, target.CenterVector3.z);
            Vector2 direction2D = (targetPos2D - casterPos2D);

            if (direction2D.sqrMagnitude < 0.0001f)
            {
                if (headMote != null && !headMote.Destroyed) { headMote.Destroy(); headMote = null; }
                return;
            }
            direction2D.Normalize();

            Vector2 logicalOrigin2D = casterPos2D + direction2D * 1.0f;
            Vector2 perpendicularDir2D = new Vector2(direction2D.y, -direction2D.x);
            float initialOffset = -(totalBeamWidth - 1) / 2.0f;
            float renderY = AltitudeLayer.MoteOverhead.AltitudeFor();

            // 2. --- 头部 Mote (固定渲染) ---
            if (headMote == null || headMote.Destroyed)
            {
                headMote = SpawnMote(Mote_Head) as Mote_Beam;
            }
            if (headMote != null)
            {
                Vector2 head_start_2D = logicalOrigin2D - direction2D * 0.5f;
                Vector2 head_end_2D = logicalOrigin2D + direction2D * 0.5f;
                headMote.UpdateBeam(
                    new Vector3(head_start_2D.x, renderY, head_start_2D.y),
                    new Vector3(head_end_2D.x, renderY, head_end_2D.y)
                );
                headMote.linearScale = new Vector3(totalBeamWidth, 1f, headMote.linearScale.z);
            }

            // 3. --- 路径预分析：计算所有终点并分组 ---
            var events = new Dictionary<float, List<int>>();
            for (int i = 0; i < totalBeamWidth; i++)
            {
                float offsetMagnitude = initialOffset + i;
                Vector2 subBeamStart2D = logicalOrigin2D + perpendicularDir2D * offsetMagnitude;
                float collisionDistance = CalculateSubBeamCollisionDistance(subBeamStart2D, direction2D);

                if (!events.ContainsKey(collisionDistance))
                {
                    events[collisionDistance] = new List<int>();
                }
                events[collisionDistance].Add(i);
            }

            // 4. --- 分层分段构建 (无缝拼接版) ---
            var sortedEventDistances = events.Keys.ToList();
            sortedEventDistances.Sort();

            HashSet<int> activeSubBeams = new HashSet<int>(Enumerable.Range(0, totalBeamWidth));
            // 追踪上一段中段的结束位置，初始为头部Mote的结束位置
            float lastMiddleSegmentEnd = 0.5f;

            foreach (float eventDistance in sortedEventDistances)
            {
                // --- A. 渲染从上一段结束到当前事件点之间的“中段” ---
                float currentMiddleSegmentEnd = eventDistance - 0.5f;

                if (currentMiddleSegmentEnd > lastMiddleSegmentEnd && activeSubBeams.Any())
                {
                    RenderBlocks(activeSubBeams, lastMiddleSegmentEnd, currentMiddleSegmentEnd, Mote_Middle, middleMotes);
                }

                // --- B. 在事件点处，为“终结者”渲染尾部 ---
                List<int> terminatingIndices = events[eventDistance];
                RenderBlocks(terminatingIndices, eventDistance - 0.5f, eventDistance + 0.5f, Mote_Tail, tailMotes);

                // --- C. 更新状态 ---
                activeSubBeams.ExceptWith(terminatingIndices);
                // 【核心变更】更新上一段中段的结束位置，为下一段无缝拼接做准备
                lastMiddleSegmentEnd = currentMiddleSegmentEnd;
            }

            // --- 辅助渲染方法 ---
            void RenderBlocks(IEnumerable<int> indices, float startDist, float endDist, ThingDef moteDef, List<Mote_Beam> listToAddTo)
            {
                if (!indices.Any() || startDist >= endDist) return;

                List<List<int>> blocks = FindContiguousBlocks(indices);
                foreach (var block in blocks)
                {
                    int segmentWidth = block.Count;
                    float startOffset = initialOffset + block.First();
                    float endOffset = initialOffset + block.Last();
                    float centerOffset = (startOffset + endOffset) / 2.0f;
                    Vector2 segmentAxisOffset2D = perpendicularDir2D * centerOffset;

                    Vector2 start_2D = logicalOrigin2D + segmentAxisOffset2D + direction2D * startDist;
                    Vector2 end_2D = logicalOrigin2D + segmentAxisOffset2D + direction2D * endDist;

                    Mote_Beam mote = SpawnMote(moteDef) as Mote_Beam;
                    if (mote != null)
                    {
                        mote.UpdateBeam(
                            new Vector3(start_2D.x, renderY, start_2D.y),
                            new Vector3(end_2D.x, renderY, end_2D.y)
                        );
                        mote.linearScale = new Vector3(segmentWidth, 1f, mote.linearScale.z);
                        listToAddTo.Add(mote);
                    }
                }
            }
        }

        // 辅助方法：寻找连续的索引块 (无变化)
        private List<List<int>> FindContiguousBlocks(IEnumerable<int> indices)
        {
            var blocks = new List<List<int>>();
            if (!indices.Any()) return blocks;

            var sortedIndices = indices.ToList();
            sortedIndices.Sort();

            List<int> currentBlock = new List<int> { sortedIndices[0] };
            blocks.Add(currentBlock);

            for (int i = 1; i < sortedIndices.Count; i++)
            {
                if (sortedIndices[i] == sortedIndices[i - 1] + 1)
                {
                    currentBlock.Add(sortedIndices[i]);
                }
                else
                {
                    currentBlock = new List<int> { sortedIndices[i] };
                    blocks.Add(currentBlock);
                }
            }
            return blocks;
        }

        // 2D碰撞检测，返回距离 (无变化)
        private float CalculateSubBeamCollisionDistance(Vector2 start2D, Vector2 direction2D)
        {
            Vector3 start3D = new Vector3(start2D.x, 0, start2D.y);
            Vector3 end3D = start3D + new Vector3(direction2D.x, 0, direction2D.y) * this.maxRange;

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(start3D.ToIntVec3(), end3D.ToIntVec3()))
            {
                if (!cell.InBounds(this.Map)) break;

                Building edifice = cell.GetEdifice(this.Map);
                if (edifice != null && edifice.def.Fillage == FillCategory.Full)
                {
                    var obstacleBounds = new Bounds(cell.ToVector3Shifted(), Vector3.one);
                    var ray = new Ray(start3D, new Vector3(direction2D.x, 0, direction2D.y));
                    if (obstacleBounds.IntersectRay(ray, out float intersectionDistance))
                    {
                        return intersectionDistance;
                    }
                    return Vector2.Distance(start2D, new Vector2(cell.x + 0.5f, cell.z + 0.5f));
                }
            }

            return this.maxRange;
        }

        private void ApplyDamage()
        {
            // 【核心修正】在调用时传入控制器所在的地图
            List<IntVec3> allEffectiveCells = AstralBeamUtility.GetAffectedCells(
                this.caster.Position.ToVector3Shifted(),
                this.target,
                this.Map, // <<-- 传入地图
                this.totalBeamWidth,
                this.maxRange,
                this.caster
            );

            // 执行爆炸伤害 (逻辑不变)
            if (allEffectiveCells.Any())
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
                    overrideCells: allEffectiveCells,
                    damageFalloff: false,
                    doVisualEffects: false,
                    doSoundEffects: false,
                    screenShakeFactor: 0.1f
                );
            }
        }

        // SpawnMote方法保持不变，确保它能正确生成Mote
        private Mote SpawnMote(ThingDef moteDef)
        {
            if (moteDef == null) return null;
            Mote mote = (Mote)ThingMaker.MakeThing(moteDef);
            GenSpawn.Spawn(mote, caster.Position, this.Map);
            return mote;
        }
        // << ==================== 区域结束 ==================== >>

        private void CleanUpMotes(List<Mote_Beam> moteList)
        {
            foreach (var mote in moteList)
            {
                if (mote != null && !mote.Destroyed) mote.Destroy();
            }
            moteList.Clear();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (headMote != null && !headMote.Destroyed) headMote.Destroy();
            CleanUpMotes(middleMotes);
            CleanUpMotes(tailMotes);
            base.Destroy(mode);
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
            Scribe_Values.Look(ref maxRange, "maxRange");
            Scribe_Values.Look(ref ticksCounter, "ticksCounter");
            Scribe_Values.Look(ref ticksUntilNextPulse, "ticksUntilNextPulse");
            Scribe_References.Look(ref headMote, "headMote");
        }
    }
}