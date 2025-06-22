using UnityEngine;
using Verse;

namespace MoManaCha_Astral_Prayer
{
    [StaticConstructorOnStartup]
    public class Mote_Beam : Mote
    {
        private Vector3 start;
        private Vector3 end;

        public void Initialize(Vector3 start, Vector3 end, float width)
        {
            this.start = start;
            this.end = end;
            this.linearScale = new Vector3(width, 1f, (end - start).magnitude);
            UpdatePositionAndRotation();
        }

        // 我们需要在每一帧都更新，以防施法者或目标移动
        public void UpdateBeam(Vector3 newStart, Vector3 newEnd)
        {
            this.start = newStart;
            this.end = newEnd;
            this.linearScale = new Vector3(this.linearScale.x, 1f, (newEnd - newStart).magnitude); // 更新长度
            UpdatePositionAndRotation();
        }

        private void UpdatePositionAndRotation()
        {
            // Mote的位置是光束的几何中心
            this.exactPosition = (start + end) / 2f;
            this.exactPosition.y = base.def.altitudeLayer.AltitudeFor();

            // Mote的旋转是光束的方向
            this.exactRotation = (end - start).AngleFlat();
        }

        // 存档和读档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref end, "end");
        }
    }
}