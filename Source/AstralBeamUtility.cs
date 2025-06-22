using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    [StaticConstructorOnStartup] // 添加这个以确保静态字段被初始化
    public static class AstralBeamUtility
    {
        // 提取出来的子光束碰撞检测逻辑
        private static float CalculateSubBeamCollisionDistance(Vector2 start2D, Vector2 direction2D, Map map, float maxRange)
        {
            Vector3 start3D = new Vector3(start2D.x, 0, start2D.y);
            Vector3 end3D = start3D + new Vector3(direction2D.x, 0, direction2D.y) * maxRange;

            // 使用 GenSight.PointsOnLineOfSight 来遍历格子
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(start3D.ToIntVec3(), end3D.ToIntVec3()))
            {
                if (!cell.InBounds(map)) break;

                Building edifice = cell.GetEdifice(map);
                if (edifice != null && edifice.def.Fillage == FillCategory.Full)
                {
                    // 使用精确的射线与边界框求交来获得更准确的距离
                    var obstacleBounds = new Bounds(cell.ToVector3Shifted(), Vector3.one);
                    var ray = new Ray(start3D, new Vector3(direction2D.x, 0, direction2D.y));
                    if (obstacleBounds.IntersectRay(ray, out float intersectionDistance))
                    {
                        return intersectionDistance;
                    }
                    // 如果求交失败（不太可能），回退到使用格子中心距离
                    return Vector2.Distance(start2D, new Vector2(cell.x + 0.5f, cell.z + 0.5f));
                }
            }

            return maxRange; // 没有碰到任何东西
        }

        // 【新增】公开的过载条件检查方法
        public static bool IsOverloadCondition(Pawn caster, LocalTargetInfo target, Map map, int totalBeamWidth, float maxRange, float minSafeDistance)
        {
            Vector2 casterPos2D = new Vector2(caster.DrawPos.x, caster.DrawPos.z);
            Vector2 targetPos2D = new Vector2(target.CenterVector3.x, target.CenterVector3.z);
            Vector2 direction2D = (targetPos2D - casterPos2D);

            if (direction2D.sqrMagnitude < 0.0001f) return false; // 没有方向，不认为是过载
            direction2D.Normalize();

            Vector2 perpendicularDir2D = new Vector2(direction2D.y, -direction2D.x);
            float initialOffset = -(totalBeamWidth - 1) / 2.0f;

            // 遍历所有子光束的起始位置
            for (int i = 0; i < totalBeamWidth; i++)
            {
                float offsetMagnitude = initialOffset + i;
                // 【重要】过载检查的起点是施法者本身，而不是前移一格的逻辑起点
                Vector2 subBeamStart2D = casterPos2D + perpendicularDir2D * offsetMagnitude;

                float collisionDistance = CalculateSubBeamCollisionDistance(subBeamStart2D, direction2D, map, maxRange);

                // 只要有一条子光束被阻挡得太近，就触发过载
                if (collisionDistance < minSafeDistance)
                {
                    return true; // 是过载状态
                }
            }

            return false; // 不是过载状态
        }


        // GetAffectedCells 方法保持不变，但为了完整性一并提供
        public static List<IntVec3> GetAffectedCells(Vector3 casterPos3D, LocalTargetInfo target, Map map, int totalBeamWidth, float maxRange, Pawn casterPawnForExclusion = null)
        {
            HashSet<IntVec3> allEffectiveCells = new HashSet<IntVec3>();

            if (map == null)
            {
                Log.ErrorOnce("AstralBeamUtility.GetAffectedCells was called with a null map.", 133742069);
                return allEffectiveCells.ToList();
            }

            Vector2 casterPos2D = new Vector2(casterPos3D.x, casterPos3D.z);
            Vector2 targetPos2D = new Vector2(target.Cell.x + 0.5f, target.Cell.z + 0.5f);
            Vector2 direction2D = (targetPos2D - casterPos2D);

            if (direction2D.sqrMagnitude < 0.0001f)
            {
                return allEffectiveCells.ToList();
            }
            direction2D.Normalize();

            Vector2 logicalOrigin2D = casterPos2D + direction2D * 1.0f;
            Vector2 perpendicularDir2D = new Vector2(direction2D.y, -direction2D.x);
            float initialOffset = -(totalBeamWidth - 1) / 2.0f;

            for (int i = 0; i < totalBeamWidth; i++)
            {
                float offsetMagnitude = initialOffset + i;
                Vector2 subBeamStart2D = logicalOrigin2D + perpendicularDir2D * offsetMagnitude;
                Vector3 start3D = new Vector3(subBeamStart2D.x, 0, subBeamStart2D.y);
                Vector3 end3D = start3D + new Vector3(direction2D.x, 0, direction2D.y) * maxRange;

                foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(start3D.ToIntVec3(), end3D.ToIntVec3()))
                {
                    if (!cell.InBounds(map)) break;
                    if (casterPawnForExclusion != null && cell == casterPawnForExclusion.Position) continue;
                    allEffectiveCells.Add(cell);
                    Building edifice = cell.GetEdifice(map);
                    if (edifice != null && edifice.def.Fillage == FillCategory.Full)
                    {
                        break;
                    }
                }
            }
            return allEffectiveCells.ToList();
        }
    }
}