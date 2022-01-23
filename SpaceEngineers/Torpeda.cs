using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineers1
{
    public sealed class Program : MyGridProgram
    {
        IMyRemoteControl Rc;
        IMyTerminalBlock Merge;
        //IMyTextPanel Lcd;
        List<IMyTerminalBlock> Trusters = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> Gyros = new List<IMyTerminalBlock>();

        int Delay = 30;
        int Tick = 0;
        float GyroMult = 1f;
        bool Start = false;

        Vector3D target = new Vector3D(60857.50, 4313, 116);

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            //Lcd = GridTerminalSystem.GetBlockWithName("Lcd") as IMyTextPanel;
            Rc = GridTerminalSystem.GetBlockWithName("RemCon") as IMyRemoteControl;
            Merge = GridTerminalSystem.GetBlockWithName("Merge");

            var trusterBlocks = GridTerminalSystem.GetBlockGroupWithName("Trusters");
            trusterBlocks.GetBlocks(Trusters);
            var gyrosBlocks = GridTerminalSystem.GetBlockGroupWithName("Gyros");
            gyrosBlocks.GetBlocks(Gyros);

            Echo("Init");
            Echo($"TrustersCnt: {Trusters.Count}");
            Echo($"GyroCnt: {Gyros.Count}");
            Echo($"RemCon: {(Rc != null ? "OK" : "Error")}");
            Echo($"Merge: {(Merge != null ? "OK" : "Error")}");
        }

        public void Main(string args, UpdateType updateSource)
        {
            Tick++;
            switch (args)
            {
                case "start":
                    Start = true;
                    Tick = 0;
                    Merge.ApplyAction("OnOff_Off");
                    foreach (var item in Trusters)
                    {
                        item.ApplyAction("OnOff_On");
                    }
                    
                    break;

                case "stop":
                    Start = false;
                    foreach (var item in Trusters)
                    {
                        item.ApplyAction("OnOff_Off");
                    }
                    SetGyroOverride(Vector3D.Zero, 0);
                    break;

                default:
                    break;
            }

            //Lcd.WriteText($"Tick: {Tick}");

            if (Start && Tick > Delay)
            {
                var set = GetNavAngles(target) * GyroMult;
                SetGyroOverride(set);
                //Lcd.WriteText($"P:{set.X}\nY:{set.Y}\nR:{set.Z}");
            }

        }

        Vector3D GetNavAngles(Vector3D target)
        {
            Vector3D center = Rc.GetPosition();
            Vector3D fow = Rc.WorldMatrix.Forward;
            Vector3D up = Rc.WorldMatrix.Up;
            Vector3D left = Rc.WorldMatrix.Left;
            Vector3D grav = -Rc.GetNaturalGravity();

            Vector3D targetNorm = Vector3D.Normalize(target - center);

            double pitch = Math.Acos(Vector3D.Dot(up, targetNorm)) - (Math.PI / 2);
            double yaw =  Math.Acos(Vector3D.Dot(left, targetNorm)) - (Math.PI / 2);
            double roll = Math.Acos(Vector3D.Dot(grav, left)) - (Math.PI / 2);

            return new Vector3D(-pitch, yaw, -roll);
        }


        void SetGyroOverride(Vector3D settings, float power = 1)
        {
            foreach (var item in Gyros)
            {
                var gyro = item as IMyGyro;
                if(gyro != null)
                {
                    gyro.GyroOverride = true;
                    gyro.GyroPower = power;
                    gyro.Pitch = (float)settings.GetDim(0);
                    gyro.Yaw = (float)settings.GetDim(1);
                    gyro.Roll = (float)settings.GetDim(2);
                }
            }
        }

    }
}
