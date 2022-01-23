using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersEnemySpotter
{
    public sealed class Program : MyGridProgram
    {
        enum State
        {
            STOP, SCAN, TRACK

        }

        IMyTextPanel LCD;
        MyDetectedEntityInfo target;
        List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
        IMyRadioAntenna Antenna;
        int distance = 10000;
        int tick = 0;
        State state = State.STOP;
        bool isSending = false;


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Antenna = GridTerminalSystem.GetBlockWithName("Antenna") as IMyRadioAntenna;
            LCD = GridTerminalSystem.GetBlockWithName("LCD") as IMyTextPanel;
            GridTerminalSystem.GetBlocksOfType(cameras);
            Echo($"Cams: {cameras.Count}");
            LCD.WriteText("Ready", false);
        }

        public void Main(string args, UpdateType updateSource)
        {
            if (args == "stop")
            {
                state = State.STOP;
                target = default(MyDetectedEntityInfo);
                SetRaycast(false);
                isSending = false;
            }
            if (args == "scan")
            {
                state = State.SCAN;
                SetRaycast(true);
            }

            if(args == "send")
            {
                isSending= true;
            }

            switch (state)
            {
                case State.STOP:
                    LCD.WriteText("Stopped");
                    break;

                case State.SCAN:
                    var cam = GetFreeCamera();
                    if (cam == null) return;
                    LCD.WriteText($"Scanning ({(int)cam.AvailableScanRange / 1000}km)");
                    Scan(cam);
                    break;

                case State.TRACK:
                    var trackCam = GetFreeCamera();
                    if (trackCam == null) return;
                    Track(trackCam, target.Position);
                    if (isSending)
                    {
                        var msg = $"init;{target.Position.X};{target.Position.Y};{target.Position.Z};" +
                            $"{target.Velocity.X};{target.Velocity.Y};{target.Velocity.Z}";
                        
                        IGC.SendBroadcastMessage("Ch1", $"{msg}", TransmissionDistance.AntennaRelay);
                    }

                    break;
            }
        }

        private void Track(IMyCameraBlock camera, Vector3D pos)
        {
            LCD.WriteText($"Scaning ({(int)camera.AvailableScanRange / 1000}km)\n");
            var info = camera.Raycast(pos);
            if (!info.IsEmpty())
            {
                if (info.EntityId == target.EntityId && info.Type == MyDetectedEntityType.LargeGrid || info.Type == MyDetectedEntityType.SmallGrid)
                {
                    var text = $"Name: {target.Name}\n" +
                            $"pos: {target.Position}\n" +
                            $"vel: {target.Velocity.Length()}\n" +
                            $"dist:{(int)Vector3D.Distance(target.Position, camera.GetPosition())}";
                    LCD.WriteText(text,true);

                    state = State.TRACK;
                    target = info;
                }
            }
            else
            {
                LCD.WriteText($"Target ({target.Name}) lost for {tick++} ticks\n");
            }
        }

        private void Scan(IMyCameraBlock camera)
        {
            var info = camera.Raycast(distance, 0, 0);
            if (!info.IsEmpty())
            {
                if (info.Type == MyDetectedEntityType.LargeGrid || info.Type == MyDetectedEntityType.SmallGrid)
                {
                    state = State.TRACK;
                    target = info;
                }
            }
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
