using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersTorpeda2
{
    public sealed class Program : MyGridProgram
    {
        public Vector3D MyPos;
        public Vector3D MyPrevPos;
        public Vector3D MyVelocity;
        public Vector3D InterceptVector;
        public double TargetDistance;


        IMyRemoteControl Rc;
        List<IMyTerminalBlock> Trusters = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> Gyros = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> Warheads = new List<IMyTerminalBlock>();

        float GyroMult = 0.5f;
        bool Start = false;
        bool isWarheadInit = false;

        List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
        Vector3D target = new Vector3D(-49870, -76434, -58094);
        Vector3D targetVel;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            IGC.RegisterBroadcastListener("Ch1");
            IGC.GetBroadcastListeners(listeners);

            Rc = GridTerminalSystem.GetBlockWithName("RemCon") as IMyRemoteControl;

            var trusterBlocks = GridTerminalSystem.GetBlockGroupWithName("Thrusters");
            trusterBlocks.GetBlocks(Trusters);
            var gyrosBlocks = GridTerminalSystem.GetBlockGroupWithName("Gyros");
            gyrosBlocks.GetBlocks(Gyros);
            var warheads = GridTerminalSystem.GetBlockGroupWithName("Warheads");
            warheads.GetBlocks(Warheads);
            
            Echo($"Thrusters: {Trusters.Count}");
            Echo($"Gyros: {Gyros.Count}");
            Echo($"Warheads: {Warheads.Count}");
        }

        public void Main(string args, UpdateType updateSource)
        {
            switch (args)
            {
                case "start":
                    Start = true;
                    foreach (IMyThrust item in Trusters)
                    {
                        //item.ThrustOverridePercentage = 100f;
                        //item.ApplyAction("OnOff_On");
                    }
                    
                    break;

                case "stop":
                    Start = false;
                    foreach (var item in Trusters)
                    {
                        item.ApplyAction("OnOff_Off");
                    }
                    SetGyroOverride(false, Vector3D.Zero);
                    break;

                default:
                    break;
            }

            if (listeners.Any() && listeners.FirstOrDefault().HasPendingMessage)
            {
                MyIGCMessage message = new MyIGCMessage();
                message = listeners[0].AcceptMessage();
                string messagetext = message.Data.ToString();

                var msg = messagetext.Split(';');
                var cmd = msg.FirstOrDefault();

                if (cmd == "init")
                {
                    if (!isWarheadInit)
                    {
                        foreach (IMyWarhead item in Warheads)
                        {
                            item.StartCountdown();
                        }
                    }

                    target = new Vector3D(Convert.ToDouble(msg[1]), Convert.ToDouble(msg[2]), Convert.ToDouble(msg[3]));
                    targetVel = new Vector3D(Convert.ToDouble(msg[4]), Convert.ToDouble(msg[5]), Convert.ToDouble(msg[6]));
                    Start = true;
                    foreach (IMyThrust item in Trusters)
                    {
                        item.ApplyAction("OnOff_On");
                        item.ThrustOverridePercentage = 100f;
                    }
                }
            }

            if (Start)
            {
                MyPos = Rc.GetPosition();
                MyVelocity = (MyPos - MyPrevPos) * 60;
                MyPrevPos = MyPos;
                TargetDistance = Vector3D.Distance(target, MyPos);
                InterceptVector = FindInterceptVector(MyPos, MyVelocity.Length(), target, targetVel);
                //var set = GetNavAngles(target) * GyroMult;
                var set = GetNavAngles2(InterceptVector) * GyroMult;
                SetGyroOverride(true, set);

                if (TargetDistance < 30)
                {
                    Detonate();
                }
            }

        }

        private void Detonate()
        {
            foreach (IMyWarhead item in Warheads)
            {
                item.Detonate();
            }
        }
        private Vector3D GetNavAngles2(Vector3D Target)
        {
            Vector3D V3Dfow = Rc.WorldMatrix.Forward;
            Vector3D V3Dup = Rc.WorldMatrix.Up;
            Vector3D V3Dleft = Rc.WorldMatrix.Left;

            Vector3D TargetNorm = Vector3D.Normalize(Target);
            Vector3D VectorReject = Vector3D.Reject(Vector3D.Normalize(MyVelocity), TargetNorm);
            Vector3D CorrectionVector = Vector3D.Normalize(TargetNorm - VectorReject * 2);

            double TargetPitch = Vector3D.Dot(V3Dup, Vector3D.Normalize(Vector3D.Reject(CorrectionVector, V3Dleft)));
            TargetPitch = Math.Acos(TargetPitch) - Math.PI / 2;
            double TargetYaw = Vector3D.Dot(V3Dleft, Vector3D.Normalize(Vector3D.Reject(CorrectionVector, V3Dup)));
            TargetYaw = Math.Acos(TargetYaw) - Math.PI / 2;
            double RollMult = Math.Abs(TargetYaw) + Math.Abs(TargetPitch);
            double TargetRoll = Math.Min(1 / RollMult, 30);
            if (RollMult > 0.3f)
                TargetRoll = 0;

            return new Vector3D(TargetYaw, -TargetPitch, TargetRoll);
        }

        private Vector3D FindInterceptVector(Vector3D shotOrigin, double shotSpeed, Vector3D targetOrigin, Vector3D targetVel)
        {
            Vector3D dirToTarget = Vector3D.Normalize(targetOrigin - shotOrigin);
            Vector3D targetVelOrth = Vector3D.Dot(targetVel, dirToTarget) * dirToTarget;
            Vector3D targetVelTang = targetVel - targetVelOrth;
            Vector3D shotVelTang = targetVelTang;
            double shotVelSpeed = shotVelTang.Length();

            if (shotVelSpeed > shotSpeed)
            {
                return Vector3D.Normalize(targetVel) * shotSpeed;
            }
            else
            {
                double shotSpeedOrth = Math.Sqrt(shotSpeed * shotSpeed - shotVelSpeed * shotVelSpeed);
                Vector3D shotVelOrth = dirToTarget * shotSpeedOrth;
                return shotVelOrth + shotVelTang;
            }
        }

        Vector3D GetNavAngles(Vector3D target)
        {
            Vector3D center = Rc.GetPosition();
            Vector3D fow = Rc.WorldMatrix.Forward;
            Vector3D up = Rc.WorldMatrix.Up;
            Vector3D left = Rc.WorldMatrix.Left;

            Vector3D targetNorm = Vector3D.Normalize(target - center);

            double pitch = Math.Acos(Vector3D.Dot(up, targetNorm)) - (Math.PI / 2);
            double yaw = Math.Acos(Vector3D.Dot(left, targetNorm)) - (Math.PI / 2);
            double roll = 0;// Math.Acos(Vector3D.Dot(fow, left)) - (Math.PI / 2);

            return new Vector3D(yaw, -pitch, roll);
        }

        public void SetGyroOverride(bool OverrideOnOff, Vector3 settings, float Power = 1)
        {
            foreach(IMyGyro gyro in Gyros)
            {
                gyro.GyroOverride = OverrideOnOff;
                gyro.GyroPower = Power;
                gyro.Yaw = (float)settings.GetDim(0);
                gyro.Pitch = -(float)settings.GetDim(1);
                gyro.Roll = (float)settings.GetDim(2) / 5;
            }
        }

    }
}
