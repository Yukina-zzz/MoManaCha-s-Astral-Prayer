using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    // 护盾的属性
    public class CompProperties_WeaponShield : CompProperties
    {
        // --- 视觉效果 ---
        public float minDrawSize = 1.2f;            // 护盾视觉效果最小尺寸
        public float maxDrawSize = 1.8f;            // 护盾视觉效果最大尺寸

        // --- 核心数值 ---
        public float energyLossPerDamage = 1.0f;    // 每点伤害消耗的能量 (可以大于1，表示护盾较脆弱)
        public int ticksToReset = 3200;             // 护盾破碎后重置所需的时间 (游戏刻) (标准护盾腰带是3200)
        public float energyOnReset = 0.2f;          // 重置后恢复的能量百分比 (例如0.2代表20%)

        // --- 特效与音效 ---
        public int hitEffectFadeoutTicks = 8;        // 护盾被击中后涟漪效果的持续时间
        public float hitEffectDisplacement = 0.05f;   // 涟漪效果的位移幅度
        public SoundDef soundAbsorb = null; // 吸收伤害的音效
        public SoundDef soundReset = null;         // 护盾重置的音效
        public EffecterDef effecterBreak = null;      // 护盾破碎的特效
        public Color shieldColor = new Color(0.5f, 0.8f, 0.9f);       //护盾颜色

        public CompProperties_WeaponShield()
        {
            compClass = typeof(CompWeaponShield);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            // 检查每个字段，如果XML没有给它赋值（即它还是null），
            // 就在这里赋上我们的默认值。
            if (this.soundAbsorb == null)
            {
                this.soundAbsorb = SoundDefOf.EnergyShield_AbsorbDamage;
            }
            if (this.soundReset == null)
            {
                this.soundReset = SoundDefOf.EnergyShield_Reset;
            }
            if (this.effecterBreak == null)
            {
                this.effecterBreak = EffecterDefOf.Shield_Break;
            }
        }
    }

    //多模式的属性
    public class CompProperties_MultiMode : CompProperties
    {

        public List<ModeDef> modes = new List<ModeDef>();
        public string uiIconPath;

        public CompProperties_MultiMode()
        {
            this.compClass = typeof(CompMultiModeWeapon);
        }

    }

    //天降落星的属性
    public class SkyfallProjectileProperties : DefModExtension
    {
        // --- Projectile_SkyfallExplosive 属性 ---

        // 飞行轨迹的起始点
        public float StartPosOffsetZ = 20f; // 从目标点后方多远的地方开始飞

        // 飞行轨迹的水平随机偏移范围。
        public float StartPosOffsetX = 5f;

        // 飞行速度(越高越慢)
        public float StartPosOffsetY = 10f;

        // 每Tick旋转的角度。正数为顺时针，负数为逆时针。
        public float rotationSpeed = 0f;

        // 轨迹特效的生成间隔（每多少Tick一次）
        public int trailFleckInterval = 5;
        // 轨迹线的大小
        public float trailLineScale = 0.5f;
        // 轨迹烟雾的大小
        public float trailSmokeScale = 0.3f;

        // 星尘特效的生成间隔
        public int stardustFleckInterval = 8;
        // 星尘特效的扩散半径
        public float stardustSpreadRadius = 0.8f;
        // 星尘特效的Y轴扩散范围
        public float stardustVerticalSpread = 0.3f;
        // 星尘特效的大小
        public float stardustScale = 0.6f;
        // 星尘的颜色
        public Color stardustColor = Color.yellow;


        // --- Projectile_SkyfallExplosiveEnhanced 属性 ---

        // 目标瞄准圈闪光的大小
        public float targetingCircleFlashScale = 1.5f;
        // 目标瞄准圈特效的数量
        public int targetingCircleFleckCount = 12;
        // 目标瞄准圈相对于爆炸半径的缩放因子
        public float targetingCircleRadiusFactor = 0.8f;
        // 目标瞄准圈烟雾的大小
        public float targetingCircleSmokeScale = 0.8f;

        // 最终撞击闪光的大小
        public float finalImpactFlashScale = 2.5f;
        // 最终撞击特效的数量
        public int finalImpactFleckCount = 15;
        // 最终撞击尘埃的扩散距离范围 (x=min, y=max)
        public Vector2 finalImpactDustDistanceRange = new Vector2(1f, 2.5f);
        // 最终撞击尘埃的大小
        public float finalImpactDustScale = 0.9f;
        // 最终撞击尘埃的颜色
        public Color finalImpactDustColor = Color.white;

    }

    // 光炮的属性
    public class CompProperties_AbilityAstralBeam : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityAstralBeam()
        {
            this.compClass = typeof(CompAbilityEffect_AstralBeam);
        }
    }
}
