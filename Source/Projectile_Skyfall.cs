// 引入必要的命名空间
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// 定义Mod的命名空间
namespace MoManaCha_Astral_Prayer
{
    /// <summary>
    /// 自定义的从天而降子弹类。
    /// 其发射逻辑被修改为从目标正上方的高空开始，垂直下落。
    /// </summary>
    public class Projectile_SkyfallExplosive : Projectile_Explosive
    {
        /// <summary>
        /// 缓存从Def中获取的自定义属性，以提高性能。
        /// </summary>
        private SkyfallProjectileProperties props;

        /// <summary>
        /// 提供对自定义属性的安全访问。
        /// 如果在XML中未定义ModExtension，则会使用默认值并记录一次错误，以防止程序崩溃。
        /// </summary>
        protected SkyfallProjectileProperties Props
        {
            get
            {
                if (props == null)
                {
                    props = def.GetModExtension<SkyfallProjectileProperties>();
                    if (props == null)
                    {
                        // 使用ErrorOnce确保只在日志中报告一次此错误，避免刷屏
                        Log.ErrorOnce($"[MoManaCha_Astral_Prayer] Projectile Def '{def.defName}' is missing the SkyfallProjectileProperties modExtension. Using default values.", def.GetHashCode());
                        // 创建一个带有默认值的实例，以防止后续代码出现空指针异常
                        props = new SkyfallProjectileProperties();
                    }
                }
                return props;
            }
        }

        /// <summary>缓存子弹在天空中的实际起始位置。</summary>
        private Vector3 skyStartPos;

        /// <summary>标记天空位置是否已初始化，用于控制特效的绘制时机。</summary>
        private bool skyPosInitialized = false;

        /// <summary>保存原始的发射位置。虽然在此类中未使用，但保留它以防未来扩展或兼容性需要。</summary>
        private Vector3 originalOrigin;

        // 用于存储当前射弹的旋转角度
        private float rotationAngle = 0f;
        /// <summary>
        /// 重写发射方法，将子弹的起点移动到目标上方的天空中。
        /// </summary>
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            // 保存原始发射位置
            originalOrigin = origin;

            // 计算天空中的起始位置
            Vector3 targetPos = intendedTarget.Cell.ToVector3Shifted();
            float randomOffsetX = Rand.Range(-Props.StartPosOffsetX, Props.StartPosOffsetX);
            skyStartPos = new Vector3(targetPos.x+ randomOffsetX, Props.StartPosOffsetY, targetPos.z+ Props.StartPosOffsetZ);

            // 使用天空位置作为新的起始点
            base.Launch(launcher, skyStartPos, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            // 标记天空位置已初始化
            skyPosInitialized = true;

        }

        /// <summary>
        /// 重写撞击方法，以确保即使是天降子弹也能正确播放其定义的爆炸音效。
        /// </summary>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 在撞击时播放XML中定义的爆炸音效
            if (def.projectile.soundExplode != null)
            {
                def.projectile.soundExplode.PlayOneShot(new TargetInfo(Position, Map, false));
            }

            base.Impact(hitThing, blockedByShield);
        }

        /// <summary>
        /// 重写绘制方法，在默认绘制逻辑之前加入自定义的天降轨迹特效。
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (skyPosInitialized)
            {
                // 绘制从天空落下的轨迹特效
                DrawSkyfallTrail();
            }

            // 调用基类方法，绘制子弹本身
            //base.DrawAt(drawLoc, flip);

            // 手动绘制带旋转的射弹，替代 base.DrawAt()
            if (this.def.graphic == null)
            {
                return; // 如果没有图形，则不绘制
            }

            // 获取射弹的图形材质
            Material material = this.Graphic.MatSingle;
            if (material == null)
            {
                return; // 如果没有材质，则不绘制
            }

            // 计算旋转
            // Quaternion.Euler的参数是(X轴旋转, Y轴旋转, Z轴旋转)
            // 我们绕Y轴旋转，产生水平旋转效果
            Quaternion rotation = Quaternion.Euler(0f, rotationAngle, 0f);

            // 创建变换矩阵，它包含了位置、旋转和缩放信息
            // def.graphic.drawSize 是一个Vector2，我们用它来设置X和Z方向的缩放
            Vector3 s = new Vector3(this.def.graphic.drawSize.x, 1f, this.def.graphic.drawSize.y);
            Matrix4x4 matrix = Matrix4x4.TRS(drawLoc, rotation, s);

            // 使用我们计算的矩阵来绘制射弹的网格
            // MeshPool.plane10 是RimWorld中用于大多数平面物体的标准网格
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }

        /// <summary>
        /// 绘制从天空落下的轨迹特效，通常是一条连接当前位置和目标地面的线。
        /// </summary>
        private void DrawSkyfallTrail()
        {
            Vector3 currentPos = DrawPos;

            // 当子弹还在空中时（Y坐标大于目标地面高度+1），才绘制轨迹
            if (currentPos.y > destination.y + 1f)
            {
                CreateTrailEffect(currentPos);
            }
        }

        /// <summary>
        /// 创建单次轨迹特效（如光线和烟雾）。通过在Tick中控制调用频率来形成连续的轨迹。
        /// </summary>
        private void CreateTrailEffect(Vector3 currentPos)
        {
            // 确保地图存在，并根据XML中定义的频率来生成特效，避免性能开销过大
            if (Map != null && Find.TickManager.TicksGame % Props.trailFleckInterval == 0)
            {
                // 创建一条从当前位置指向目标地面的光束特效
                FleckMaker.ConnectingLine(currentPos,
                    new Vector3(currentPos.x, destination.y, currentPos.z),
                    FleckDefOf.FlashHollow, Map, Props.trailLineScale);

                // 在当前位置创建一些伴随的烟雾效果
                FleckMaker.ThrowSmoke(currentPos, Map, Props.trailSmokeScale);
            }
        }

        /// <summary>
        /// 每帧调用，用于处理子弹的逻辑，如此处的特效生成。
        /// </summary>
        public override void Tick()
        {
            base.Tick();

            // 更新旋转角度
            if (Props.rotationSpeed != 0)
            {
                rotationAngle += Props.rotationSpeed;
                // 将角度保持在 0-360 之间，防止数值无限增大
                if (rotationAngle >= 360f)
                {
                    rotationAngle -= 360f;
                }
            }

            // 如果已经从天空发射并且在地图上
            if (skyPosInitialized && Map != null)
            {
                // 根据XML定义的频率，周期性地创建星尘特效
                if (Find.TickManager.TicksGame % Props.stardustFleckInterval == 0)
                {
                    CreateStardustEffect();
                }
            }
        }

        /// <summary>
        /// 创建环绕在子弹周围的星尘特效，增加视觉丰富度。
        /// </summary>
        private void CreateStardustEffect()
        {
            Vector3 currentPos = DrawPos;

            // 在子弹周围的随机位置创建几个星尘粒子
            for (int i = 0; i < 2; i++)
            {
                Vector3 dustPos = currentPos + new Vector3(
                    Rand.Range(-Props.stardustSpreadRadius, Props.stardustSpreadRadius),
                    Rand.Range(-Props.stardustVerticalSpread, Props.stardustVerticalSpread),
                    Rand.Range(-Props.stardustSpreadRadius, Props.stardustSpreadRadius)
                );

                FleckMaker.ThrowDustPuff(dustPos, Map, Props.stardustScale);
            }
        }
    }

    /// <summary>
    /// 增强版的天降子弹，继承自基础版，并增加了更多的视觉效果，
    /// 如目标预警圈和更华丽的撞击特效。
    /// </summary>
    public class Projectile_SkyfallExplosiveEnhanced : Projectile_SkyfallExplosive
    {
        /// <summary>
        /// 重写发射方法，在基类逻辑的基础上，增加一个目标预警圈效果。
        /// </summary>
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            // 在目标位置预先创建一个瞄准圈效果，警告玩家和AI
            CreateTargetingCircle(intendedTarget.Cell.ToVector3Shifted());
        }

        /// <summary>
        /// 在目标位置创建一个由闪光和环状烟雾构成的瞄准圈。
        /// </summary>
        private void CreateTargetingCircle(Vector3 targetPos)
        {
            if (Map != null)
            {
                // 在中心创建一个静态的闪光，作为视觉焦点
                FleckMaker.Static(targetPos, Map, FleckDefOf.ExplosionFlash, Props.targetingCircleFlashScale);

                // 沿圆形路径生成一系列烟雾，形成一个动态的瞄准圈
                for (int i = 0; i < Props.targetingCircleFleckCount; i++)
                {
                    // 计算每个烟雾在圆上的角度
                    float angle = i * (360f / Props.targetingCircleFleckCount) * Mathf.Deg2Rad;
                    // 根据爆炸半径和XML中定义的缩放因子计算烟雾的位置
                    Vector3 circlePos = targetPos + new Vector3(
                        Mathf.Cos(angle) * def.projectile.explosionRadius * Props.targetingCircleRadiusFactor,
                        0f, // 保持在地面上
                        Mathf.Sin(angle) * def.projectile.explosionRadius * Props.targetingCircleRadiusFactor
                    );

                    FleckMaker.ThrowDustPuff(circlePos, Map, Props.targetingCircleSmokeScale);
                }
            }
        }

        /// <summary>
        /// 重写撞击方法，在基类爆炸逻辑执行前，先创建最终的撞击特效。
        /// </summary>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 撞击前的最终视觉高潮
            CreateFinalImpactEffect();

            base.Impact(hitThing, blockedByShield);
        }

        /// <summary>
        /// 在撞击点创建强烈的最终视觉效果，如巨大的闪光和向外扩散的星尘。
        /// </summary>
        private void CreateFinalImpactEffect()
        {
            if (Map != null)
            {
                Vector3 impactPos = Position.ToVector3Shifted();

                // 创建一个比预警圈更强烈的中心闪光
                FleckMaker.Static(impactPos, Map, FleckDefOf.ExplosionFlash, Props.finalImpactFlashScale);

                // 创建大量向外随机扩散的星尘，模拟爆炸冲击波的效果
                for (int i = 0; i < Props.finalImpactFleckCount; i++)
                {
                    float angle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                    float distance = Rand.Range(Props.finalImpactDustDistanceRange.x, Props.finalImpactDustDistanceRange.y);
                    Vector3 dustPos = impactPos + new Vector3(
                        Mathf.Cos(angle) * distance,
                        0.3f,
                        Mathf.Sin(angle) * distance+0.3f
                    );

                    // 使用XML定义的颜色和大小生成星尘
                    FleckMaker.ThrowDustPuff(dustPos, Map, Props.finalImpactDustScale);
                }
            }
        }
    }
}