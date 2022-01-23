using Sandbox.ModAPI.Ingame;
using System.Text;
using VRageMath;

namespace SpaceEngineers2
{
    public sealed class Program : MyGridProgram
    {

        IMyTextPanel Lcd;
        List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
        int tick, raycastCount;
        int distance = 70000;
        float PITCH = 5f;
        float YAW = 5f;
        float pitch, yaw;
        int currentEntity = 0;

        Random r = new Random();

        private MyDetectedEntityInfo info;
        bool scanning = false;
        private StringBuilder sb = new StringBuilder();

        List<MyDetectedEntityInfo> detected = new List<MyDetectedEntityInfo>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Lcd = GridTerminalSystem.GetBlockWithName("LCD2") as IMyTextPanel;

            GridTerminalSystem.GetBlocksOfType(cameras);

            Echo("Init");
            Echo($"CameraCnt: {cameras.Count}");
        }

        public void Main(string args, UpdateType updateSource)
        {
            Lcd.WriteText($"Tick: {tick}\t CastCnt: {raycastCount} \tDetected: {detected.Count} p:{pitch} y:{yaw}\n");
            Lcd.WriteText($"CurIndex: { currentEntity}\n",true);
            switch (args)
            {
                case "scan":
                    scanning = true;
                    SetRaycast(true);

                    break;

                case "stop":
                    scanning = false;
                    break;
                case "next":
                    if (detected.Count > 0 && currentEntity < detected.Count-1) currentEntity++;
                    break;
                case "prev":
                    if (currentEntity > 0) currentEntity--;
                    break;

                default:
                    break;
            }

            if (scanning)
            {
                pitch = (float)r.NextDouble() *  PITCH*2 -PITCH;
                yaw = (float)r.NextDouble() * YAW * 2 - YAW;

                var cam = GetFreeCamera();
                if (cam != null)
                {
                    info = cam.Raycast(distance, pitch, yaw);
                    raycastCount++;
                    if (!info.IsEmpty()) 
                    {
                        Echo("Found!!!!");
                        if (
                            //(info.Type == MyDetectedEntityType.LargeGrid ||
                            //info.Type == MyDetectedEntityType.SmallGrid ||
                            //info.Type == MyDetectedEntityType.)
                            //&& 
                            !detected.Any(x => x.EntityId == info.EntityId))
                        {
                            detected.Add(info);
                        }

                    }
                }
            }

            if (detected.Count > 0)
            {
                var cur = detected[currentEntity];

                sb.Clear();
                sb.Append("EntityID: " + cur.EntityId);
                sb.AppendLine();
                sb.Append("Name: " + cur.Name);
                sb.AppendLine();
                sb.Append("Type: " + cur.Type);
                sb.AppendLine();
                sb.Append("Velocity: " + cur.Velocity.ToString("0.000"));
                sb.AppendLine();
                sb.Append("Relationship: " + cur.Relationship);
                sb.AppendLine();
                sb.Append("Size: " + cur.BoundingBox.Size.ToString("0.000"));
                sb.AppendLine();
                sb.Append("Position: " + cur.Position.ToString("0.000"));

                if (cur.HitPosition.HasValue)
                {
                    sb.AppendLine();
                    sb.Append("Hit: " + cur.HitPosition.Value.ToString("0.000"));
                    sb.AppendLine();
                    sb.Append("Distance: " + Vector3D.Distance(Lcd.GetPosition(), cur.HitPosition.Value).ToString("0.00"));
                }

                sb.AppendLine();
                Lcd.WriteText(sb.ToString(), true);
            }


            tick++;
        }

        private void SetRaycast(bool onOff)
        {
            foreach (var cam in cameras)
            {
                cam.EnableRaycast = onOff;
            }
        }

        private IMyCameraBlock GetFreeCamera()
        {
            return cameras.FirstOrDefault(x => x.CanScan(distance));
        }

    }
}
