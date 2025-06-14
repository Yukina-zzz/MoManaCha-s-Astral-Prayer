using HarmonyLib;
using MoManaCha_Astral_Prayer;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer // 确保与上面文件的命名空间一致
{
    // 这个类负责在游戏启动时应用所有补丁
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            // 创建一个Harmony实例，ID应该是全局唯一的
            var harmony = new Harmony("MoManaCha.Pray");

            // 自动查找并应用本项目中所有的补丁
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Pawn_PreApplyDamage_Patch
    {
        /// <summary>
        /// Postfix 补丁会在原版 Pawn.PreApplyDamage 方法执行完毕后运行。
        /// 它检查伤害是否已经被其他方式（如护盾腰带）吸收。
        /// 如果没有，它会检查并激活武器上的护盾。
        /// </summary>
        /// <param name="__instance">被调用方法的Pawn实例</param>
        /// <param name="dinfo">伤害信息</param>
        /// <param name="absorbed">这是一个引用参数，允许我们修改原方法的结果</param>
        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            // 关键逻辑：如果伤害已经被原版流程吸收了（比如被护盾腰带），
            // 我们就什么都不做，直接返回。
            if (absorbed)
            {
                return;
            }

            // 确保 Pawn 有装备，并且主手武器不为空
            if (__instance.equipment?.Primary == null)
            {
                return;
            }

            // 尝试从主手武器上获取我们的护盾组件
            var weaponShield = __instance.equipment.Primary.GetComp<CompWeaponShield>();

            // 如果找到了我们的组件
            if (weaponShield != null)
            {
                // 调用我们组件的伤害处理方法。
                // 这个方法会根据结果，通过 out 参数来修改一个新的 absorbed 变量。
                // 我们再把这个新值赋给 ref 参数，从而改变整个伤害流程的结果。
                weaponShield.PostPreApplyDamage(ref dinfo, out bool weaponAbsorbed);
                if (weaponAbsorbed)
                {
                    absorbed = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Tick))]
    public static class Pawn_Tick_Patch
    {
        // Postfix 在原版 Pawn.Tick() 执行后运行
        public static void Postfix(Pawn __instance)
        {
            // 确保Pawn活着、有装备、且在地图上
            if (__instance == null || __instance.Dead || __instance.equipment?.Primary == null || !__instance.Spawned)
            {
                return;
            }

            // 检查主手武器上是否有我们的组件
            var weaponShield = __instance.equipment.Primary.GetComp<CompWeaponShield>();
            if (weaponShield != null)
            {
                // 手动调用我们的Tick逻辑
                weaponShield.ShieldTick();
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    public static class PawnRenderUtility_DrawExtras_Patch
    {
        /// <summary>
        /// Postfix 补丁在原版方法绘制完所有装备和服装特效后运行。
        /// 这是为武器添加自定义视觉效果的最佳时机。
        /// </summary>
        /// <param name="pawn">正在被渲染的Pawn</param>
        public static void Postfix(Pawn pawn) // 简化版参数，我们只需要pawn
        {
            // 同样进行安全检查
            if (pawn == null || pawn.Dead || pawn.equipment?.Primary == null || !pawn.Spawned)
            {
                return;
            }

            // 检查主手武器上是否有我们的组件
            var weaponShield = pawn.equipment.Primary.GetComp<CompWeaponShield>();
            if (weaponShield != null)
            {
                // 调用我们分离出来的、专门负责绘制的方法
                weaponShield.DrawShield();
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_Patch
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.equipment?.Primary == null)
            {
                return;
            }

            var comp = __instance.equipment.Primary.GetComp<CompMultiModeWeapon>();
            if (comp != null)
            {
                // 1. 将原始结果转换为一个可修改的列表
                List<Gizmo> gizmoList = __result.ToList();

                // 2. 将我们自己的Gizmo添加到这个列表中
                //    AddRage会添加一个IEnumerable<Gizmo>中的所有元素
                gizmoList.AddRange(comp.GetEquippedGizmos());

                // 3. 将__result指向我们这个新的、包含了所有Gizmo的列表
                __result = gizmoList;
            }
        }
    }
}