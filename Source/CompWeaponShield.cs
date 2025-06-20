﻿using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MoManaCha_Astral_Prayer // 您可以替换成自己的Mod命名空间
{
    // 这个枚举用于方便地管理护盾状态
    public enum WeaponShieldState
    {
        Active,    // 激活，可吸收伤害
        Resetting, // 破碎，充能中
        Disabled   // 武器不在人手上，或没有电力等
    }


    [StaticConstructorOnStartup]
    public class CompWeaponShield : ThingComp
    {
        // 字段
        private float energy;
        private int ticksToReset = -1;
        private int lastAbsorbDamageTick = -9999;
        private Vector3 impactAngleVect;

        // --- 独立的材质实例 ---
        [Unsaved(false)]
        private Material shieldMaterial;

        private static Texture2D shieldBubbleTexture;
        private static Texture2D ShieldBubbleTexture
        {
            get
            {
                if (shieldBubbleTexture == null)
                {
                    shieldBubbleTexture = ContentFinder<Texture2D>.Get("Other/ShieldBubble");
                }
                return shieldBubbleTexture;
            }
        }

        // 属性
        public CompProperties_WeaponShield Props => (CompProperties_WeaponShield)props;
        public float EnergyMax => parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax);
        public float EnergyGainPerTick => parent.GetStatValue(StatDefOf.EnergyShieldRechargeRate) / 60f;
        public Pawn PawnOwner => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        // --- 材质初始化方法 ---
        private void InitializeShieldMaterial()
        {
            // 如果材质已经创建，或者Props为空，则不做任何事
            if (shieldMaterial != null || Props == null)
            {
                return;
            }

            Color color = Props.shieldColor;
            // 创建全新的材质
            shieldMaterial = new Material(ShaderDatabase.MoteGlow);
            shieldMaterial.mainTexture = ShieldBubbleTexture;
            shieldMaterial.color = color;
            shieldMaterial.renderQueue = 3600;
        }

        public WeaponShieldState ShieldState
        {
            get
            {
                if (PawnOwner == null) return WeaponShieldState.Disabled;
                if (ticksToReset > 0) return WeaponShieldState.Resetting;
                return WeaponShieldState.Active;
            }
        }

        // --- 核心方法 ---
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;

            // 护盾未激活则不处理
            if (ShieldState != WeaponShieldState.Active)
            {
                return;
            }

            // 只防御远程攻击
            if (!dinfo.Def.isRanged) { return; }

            energy -= dinfo.Amount * Props.energyLossPerDamage;

            if (energy < 0f)
            {
                Break(); // 能量耗尽，护盾破碎
            }
            else
            {
                AbsorbedDamage(dinfo); // 成功吸收，播放特效
            }

            absorbed = true;
        }

        // --- 其他辅助方法 

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref energy, "energy", 0f);
            Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
            Scribe_Values.Look(ref lastAbsorbDamageTick, "lastAbsorbDamageTick", -9999);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            energy = EnergyMax;
        }

        public void ShieldTick()
        {
            if (PawnOwner == null)
            {
                // 虽然在Pawn.Tick中调用，但以防万一
                energy = 0;
                return;
            }

            if (ShieldState == WeaponShieldState.Resetting)
            {
                ticksToReset--;
                if (ticksToReset <= 0) Reset();
            }
            else if (ShieldState == WeaponShieldState.Active)
            {
                energy += EnergyGainPerTick;
                if (energy > EnergyMax) energy = EnergyMax;
            }
        }

        private void AbsorbedDamage(DamageInfo dinfo)
        {
            if (PawnOwner == null || !PawnOwner.Spawned || PawnOwner.Map == null)
            {
                // 如果Pawn无效，或未在地图上生成，则不执行任何视觉/听觉效果，直接返回。
                return;
            }
            Props.soundAbsorb?.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
            impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
            Vector3 loc = PawnOwner.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
            float num = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
            FleckMaker.Static(loc, PawnOwner.Map, FleckDefOf.ExplosionFlash, num);
            int num2 = (int)num;
            for (int i = 0; i < num2; i++)
                FleckMaker.ThrowDustPuff(loc, PawnOwner.Map, Rand.Range(0.8f, 1.2f));
            lastAbsorbDamageTick = Find.TickManager.TicksGame;
        }

        private void Break()
        {
            float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy / EnergyMax);
            Props.effecterBreak?.SpawnAttached(PawnOwner, PawnOwner.MapHeld, scale);
            FleckMaker.Static(PawnOwner.TrueCenter(), PawnOwner.Map, FleckDefOf.ExplosionFlash, 12f);
            for (int i = 0; i < 6; i++)
                FleckMaker.ThrowDustPuff(PawnOwner.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), PawnOwner.Map, Rand.Range(0.8f, 1.2f));
            energy = 0f;
            ticksToReset = Props.ticksToReset;
        }

        private void Reset()
        {
            if (PawnOwner.Spawned)
            {
                Props.soundReset?.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
                FleckMaker.ThrowLightningGlow(PawnOwner.TrueCenter(), PawnOwner.Map, 3f);
            }
            ticksToReset = -1;
            energy = EnergyMax * Props.energyOnReset;  // 重置后恢复少量能量
        }

        public void DrawShield()
        {
            // 在绘制前，确保我们的自定义材质已经被创建
            InitializeShieldMaterial();

            if (ShieldState == WeaponShieldState.Active && PawnOwner != null && PawnOwner.Map != null && shieldMaterial != null)
            {
                // 只有在征召或战斗时才绘制
                if (!PawnOwner.Drafted && !PawnOwner.IsFighting())
                {
                    return; // 如果不满足条件，直接退出，不绘制
                }
                float num = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy / EnergyMax);
                Vector3 drawPos = PawnOwner.Drawer.DrawPos;
                drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                int num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
                if (num2 < Props.hitEffectFadeoutTicks)
                {
                    float num3 = (float)(Props.hitEffectFadeoutTicks - num2) / Props.hitEffectFadeoutTicks * Props.hitEffectDisplacement;
                    drawPos += impactAngleVect * num3;
                    num -= num3;
                }
                float angle = Rand.Range(0, 360);
                Vector3 s = new Vector3(num, 1f, num);
                Matrix4x4 matrix = default;
                matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMaterial, 0);
            }
        }
    }
}