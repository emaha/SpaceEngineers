using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersSwarmController
{
    // Управление стаей ботов
    public sealed class Program : MyGridProgram
    {
        enum State
        {
            FOLLOW,
            STOP
        }

        State state = State.STOP;
        
        IMyRemoteControl RemCon;
        IMyTextPanel LCD;
        List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
        double distance = 50;
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            LCD = GridTerminalSystem.GetBlockWithName("LCD") as IMyTextPanel;
            RemCon = GridTerminalSystem.GetBlockWithName("RemCon") as IMyRemoteControl;

            IGC.RegisterBroadcastListener("Ch2");
            IGC.GetBroadcastListeners(listeners);
        }

        public void Main(string argument)
        {
            if(argument == "follow")
            {
                state = State.FOLLOW;
            }

            if (argument == "stop")
            {
                state = State.STOP;
                IGC.SendBroadcastMessage("Ch1", $"stop", TransmissionDistance.AntennaRelay);
            }

            switch (state)
            {
                case State.STOP:
                    LCD.WriteText($"STOP");

                    break;
                case State.FOLLOW:
                    var myPos = RemCon.GetPosition();
                    var dir = RemCon.WorldMatrix.GetOrientation().GetDirectionVector(Base6Directions.Direction.Forward);
                    Vector3D worldDirection = Vector3D.TransformNormal(dir, RemCon.WorldMatrix);
                    var gPos = myPos + dir * distance;
                    var pos = gPos;

                    var msg = $"follow;{pos.X};{pos.Y};{pos.Z};" +
                        $"{pos.X};{pos.Y};{pos.Z};";

                    IGC.SendBroadcastMessage("Ch1", $"{msg}", TransmissionDistance.AntennaRelay);
                    LCD.WriteText($"myPos:{myPos}\n");
                    LCD.WriteText($"pos:{gPos}\n",true);
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
                LCD.WriteText($"{messagetext}\n", true);

                switch (cmd)
                {
                    case "follow":
                        break;
                    case "point":
                        break;
                }

                
                //if (msg.FirstOrDefault() == botName)
                //{
                //    currentTargetPos = new Vector3D(Convert.ToDouble(msg[1]), Convert.ToDouble(msg[2]), Convert.ToDouble(msg[3]));
                //    state = State.MOVING;   
                //}
                
            }

        }

    }
}
