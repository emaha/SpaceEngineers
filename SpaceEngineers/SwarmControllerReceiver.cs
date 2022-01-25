using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersSwarmControllerReceiver
{
    public sealed class Program : MyGridProgram
    {
        enum State
        {
            IDLE,
            FOLLOW,
            STOP
        }

        int botId = 0;
        int tick = 0;
        float distance = 150;

        Follower Follower1;
        State state = State.STOP;

        IMyRemoteControl RemCon;
        IMyTextPanel LCD;
        List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();

        float koeffA = 3.0f;
        float koeffV = 3.0f;

        Vector3D currentTargetPos = new Vector3D(-9094, -15320, -58447);

        public Program()
        {
            Random r = new Random();

            botId = r.Next(10);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            LCD = GridTerminalSystem.GetBlockWithName("LCD") as IMyTextPanel;
            RemCon = GridTerminalSystem.GetBlockWithName("RemCon") as IMyRemoteControl;

            IGC.RegisterBroadcastListener("Ch1");
            IGC.GetBroadcastListeners(listeners);
            Follower1 = new Follower(this, koeffA, koeffV);

            Stop();
        }

        private void Stop()
        {
            state = State.STOP;
            Follower1.TurnOffOverride();
            tick = 0;
        }

        public void Main(string argument)
        {
            switch (state)
            {
                case State.STOP:
                    LCD.WriteText($"STOP: {tick++}");
                    
                    break;
                case State.IDLE:
                    LCD.WriteText($"IDLE: {tick++}");
                    
                    break;
                case State.FOLLOW:
                    var dist = Follower1.GoToPos(currentTargetPos);

                    LCD.WriteText($"Moving...Distance: {dist}%");
                    break;
            }
                
            if (listeners.Any() && listeners.FirstOrDefault().HasPendingMessage)
            {
                MyIGCMessage message = new MyIGCMessage();
                message = listeners[0].AcceptMessage();
                string messagetext = message.Data.ToString();
                string messagetag = message.Tag;
                long sender = message.Source;

                //Do something with the information!
                Echo("Message received with tag" + messagetag + "\n\r");
                Echo("from address " + sender.ToString() + ": \n\r");
                Echo(messagetext);

                
                var msg = messagetext.Split(';');
                var cmd = msg.FirstOrDefault();

                switch (cmd)
                {
                    case "follow":
                        state = State.FOLLOW;
                        currentTargetPos = new Vector3D(Convert.ToDouble(msg[1]), Convert.ToDouble(msg[2]), Convert.ToDouble(msg[3]));
                        Vector3D fow = new Vector3D(Convert.ToDouble(msg[4]), Convert.ToDouble(msg[5]), Convert.ToDouble(msg[6]));
                        break;

                    case "stop":
                        Stop();
                        break;
                }

                //if (msg.FirstOrDefault() == botName)
                //{
                //    currentTargetPos = new Vector3D(Convert.ToDouble(msg[1]), Convert.ToDouble(msg[2]), Convert.ToDouble(msg[3]));
                //    state = State.FOLLOW;   
                //}
                
            }

        }

        public class Follower
        {
            IMyRemoteControl RemCon;
            static Program ParentProgram;
            MyThrusters myThr;
            MyGyros myGyros;
            float kV;
            float kA;

            public Follower(Program parenProg, float koeffA, float koeffV)
            {
                ParentProgram = parenProg;
                kV = koeffV;
                kA = koeffA;
                InitMainBlocks();
                InitSubSystems();
            }

            private void InitMainBlocks()
            {
                RemCon = ParentProgram.GridTerminalSystem.GetBlockWithName("RemCon") as IMyRemoteControl;
            }

            private void InitSubSystems()
            {
                myThr = new MyThrusters(this);
                myGyros = new MyGyros(this, 5);
            }

            public void TurnOffOverride()
            {
                myGyros.GyroOver(false);
                myThr.SetThrF(Vector3D.Zero);
            }

            public double GoToPos(Vector3D Pos)
            {
                //myGyros.KeepHorizon();

                Vector3D left = RemCon.WorldMatrix.Left;
                Pos += -Vector3D.Normalize(left) * ParentProgram.distance;
                MatrixD MyMatrix = RemCon.WorldMatrix.GetOrientation();
                //Расчитать расстояние до цели
                Vector3D TargetVector = Pos - RemCon.GetPosition();
                Vector3D TargetVectorNorm = Vector3D.Normalize(TargetVector);
                //Расчитать желаемую скорость
                Vector3D DesiredVelocity = TargetVector * Math.Sqrt(2 * kV / TargetVector.Length());
                Vector3D VelocityDelta = DesiredVelocity - RemCon.GetShipVelocities().LinearVelocity;
                //Расчитать желаемое ускорение
                Vector3D DesiredAcceleration = VelocityDelta * kA;
                //Передаем желаемое ускорение с учетом гравитации движкам
                myThr.SetThrA(VectorTransform(DesiredAcceleration, MyMatrix));

                return Vector3D.Distance(Pos, RemCon.GetPosition());    
            }

            public void OrbitPos(Vector3D Pos, double OrbitH, double OrbitR, double OrbitV)
            {
                //Получаем вертикальный вектор
                Vector3D GravAccel = RemCon.GetNaturalGravity();
                Vector3D GravAccelNorm = Vector3D.Normalize(GravAccel);
                MatrixD MyMatrix = RemCon.WorldMatrix.GetOrientation();
                //Расчитать вектор до цели
                Vector3D TargetVector = Pos - RemCon.GetPosition();
                Vector3D TargetVectorNorm = Vector3D.Normalize(TargetVector);
                //Расчитать горизонтальный вектор до цели
                Vector3D TargetVectorHor = Vector3D.Reject(TargetVector, GravAccelNorm);
                Vector3D TargetVectorHorNorm = Vector3D.Normalize(TargetVectorHor);
                //Расчитать горизонтальную координату на орбите
                Vector3D OrbitPosHor = Pos - TargetVectorHorNorm * OrbitR;
                Vector3D OrbitPos = OrbitPosHor - GravAccelNorm * OrbitH;
                
                //Если хотим высоту над ландшафтом
                double CurrentElevation = 0;
                double DesiredElevation = OrbitH;
                RemCon.TryGetPlanetElevation(MyPlanetElevation.Surface, out CurrentElevation);
                ParentProgram.Echo(CurrentElevation.ToString());
                OrbitPos = OrbitPosHor - GravAccelNorm * (DesiredElevation - CurrentElevation);

                //Расчитать вектор до входной точки на орбите
                Vector3D OrbitVector = OrbitPos - RemCon.GetPosition();

                //Расчитать вектор скорости на орбите
                Vector3D OrbitVel = TargetVectorHorNorm.Cross(GravAccelNorm) * OrbitV;

                //Расчитать желаемую скорость
                Vector3D DesiredVelocity = OrbitVector * Math.Sqrt(2 * kV / OrbitVector.Length());

                if (OrbitVector.Length() < 1000)
                    DesiredVelocity += OrbitVel;

                Vector3D VelocityDelta = DesiredVelocity - RemCon.GetShipVelocities().LinearVelocity;
                //Расчитать желаемое ускорение
                Vector3D DesiredAcceleration = VelocityDelta * kA;
                //Передаем желаемое ускорение с учетом гравитации движкам
                myThr.SetThrA(VectorTransform(DesiredAcceleration - GravAccel, MyMatrix));
            }

            public Vector3D VectorTransform(Vector3D Vec, MatrixD Orientation)
            {
                return new Vector3D(Vec.Dot(Orientation.Right), Vec.Dot(Orientation.Up), Vec.Dot(Orientation.Backward));
            }

            private class MyGyros
            {
                IMyRemoteControl RemCon;
                List<IMyGyro> Gyros;
                float gyroMult;
                Follower myBot;

                public MyGyros(Follower mbt, float mult)
                {
                    RemCon = mbt.RemCon;
                    myBot = mbt;
                    gyroMult = mult;
                    InitMainBlocks();
                }

                private void InitMainBlocks()
                {
                    Gyros = new List<IMyGyro>();
                    ParentProgram.GridTerminalSystem.GetBlocksOfType<IMyGyro>(Gyros);
                }

                public float LookAtPoint(Vector3D LookPoint)
                {
                    Vector3D SignalVector = Vector3D.Normalize(LookPoint);
                    foreach (IMyGyro gyro in Gyros)
                    {
                        gyro.Pitch = -(float)SignalVector.Y * gyroMult;
                        gyro.Yaw = (float)SignalVector.X * gyroMult;
                        gyro.Roll = 1f;
                    }
                    return (Math.Abs((float)SignalVector.Y) + Math.Abs((float)SignalVector.X));
                }

                public void SetGyro(Vector3D axis)
                {
                    foreach (IMyGyro gyro in Gyros)
                    {
                        gyro.Yaw = (float)axis.Dot(gyro.WorldMatrix.Up);
                        gyro.Pitch = (float)axis.Dot(gyro.WorldMatrix.Right);
                        gyro.Roll = (float)axis.Dot(gyro.WorldMatrix.Backward);
                    }
                }

                public void GyroOver(bool over)
                {
                    foreach (IMyGyro gyro in Gyros)
                    {
                        gyro.Yaw = 0;
                        gyro.Pitch = 0;
                        gyro.Roll = 0;
                        gyro.GyroOverride = over;
                    }
                }

            }

            private class MyThrusters
            {
                Follower myBot;
                List<IMyThrust> AllThrusters;
                List<IMyThrust> UpThrusters;
                List<IMyThrust> DownThrusters;
                List<IMyThrust> LeftThrusters;
                List<IMyThrust> RightThrusters;
                List<IMyThrust> ForwardThrusters;
                List<IMyThrust> BackwardThrusters;

                double UpThrMax;
                double DownThrMax;
                double LeftThrMax;
                double RightThrMax;
                double ForwardThrMax;
                double BackwardThrMax;


                //переменные подсистемы двигателей
                public MyThrusters(Follower mbt)
                {
                    myBot = mbt;
                    InitMainBlocks();
                }

                private void InitMainBlocks()
                {
                    Matrix ThrLocM = new Matrix();
                    Matrix MainLocM = new Matrix();
                    myBot.RemCon.Orientation.GetMatrix(out MainLocM);

                    AllThrusters = new List<IMyThrust>();
                    UpThrusters = new List<IMyThrust>();
                    DownThrusters = new List<IMyThrust>();
                    LeftThrusters = new List<IMyThrust>();
                    RightThrusters = new List<IMyThrust>();
                    ForwardThrusters = new List<IMyThrust>();
                    BackwardThrusters = new List<IMyThrust>();
                    UpThrMax = 0;
                    DownThrMax = 0;
                    LeftThrMax = 0;
                    RightThrMax = 0;
                    ForwardThrMax = 0;
                    BackwardThrMax = 0;

                    ParentProgram.GridTerminalSystem.GetBlocksOfType<IMyThrust>(AllThrusters);

                    foreach (IMyThrust Thrust in AllThrusters)
                    {
                        Thrust.Orientation.GetMatrix(out ThrLocM);
                        //Y
                        if (ThrLocM.Backward == MainLocM.Up)
                        {
                            UpThrusters.Add(Thrust);
                            UpThrMax += Thrust.MaxEffectiveThrust;
                        }
                        else if (ThrLocM.Backward == MainLocM.Down)
                        {
                            DownThrusters.Add(Thrust);
                            DownThrMax += Thrust.MaxEffectiveThrust;
                        }
                        //X
                        else if (ThrLocM.Backward == MainLocM.Left)
                        {
                            LeftThrusters.Add(Thrust);
                            LeftThrMax += Thrust.MaxEffectiveThrust;
                        }
                        else if (ThrLocM.Backward == MainLocM.Right)
                        {
                            RightThrusters.Add(Thrust);
                            RightThrMax += Thrust.MaxEffectiveThrust;
                        }
                        //Z
                        else if (ThrLocM.Backward == MainLocM.Forward)
                        {
                            ForwardThrusters.Add(Thrust);
                            ForwardThrMax += Thrust.MaxEffectiveThrust;
                        }
                        else if (ThrLocM.Backward == MainLocM.Backward)
                        {
                            BackwardThrusters.Add(Thrust);
                            BackwardThrMax += Thrust.MaxEffectiveThrust;
                        }
                    }
                }

                private void SetGroupThrust(List<IMyThrust> ThrList, float Thr)
                {
                    foreach(IMyThrust thr in ThrList)
                    {
                        thr.ThrustOverridePercentage = Thr;
                    }
                }

                public void SetThrF(Vector3D ThrVec)
                {
                    SetGroupThrust(AllThrusters, 0f);
                    //X
                    if (ThrVec.X > 0)
                    {
                        SetGroupThrust(RightThrusters, (float)(ThrVec.X / RightThrMax));
                    }
                    else
                    {
                        SetGroupThrust(LeftThrusters, -(float)(ThrVec.X / LeftThrMax));
                    }
                    //Y
                    if (ThrVec.Y > 0)
                    {
                        SetGroupThrust(UpThrusters, (float)(ThrVec.Y / UpThrMax));
                    }
                    else
                    {
                        SetGroupThrust(DownThrusters, -(float)(ThrVec.Y / DownThrMax));
                    }
                    //Z
                    if (ThrVec.Z > 0)
                    {
                        SetGroupThrust(BackwardThrusters, (float)(ThrVec.Z / BackwardThrMax));
                    }
                    else
                    {
                        SetGroupThrust(ForwardThrusters, -(float)(ThrVec.Z / ForwardThrMax));
                    }
                }
                public void SetThrA(Vector3D ThrVec)
                {
                    double PhysMass = myBot.RemCon.CalculateShipMass().PhysicalMass;
                    SetThrF(ThrVec * PhysMass);
                }


            }

        }

        

    }
}
