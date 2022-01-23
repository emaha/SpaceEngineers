using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersMinerspotter
{
    public sealed class Program : MyGridProgram
    {
        IMyTextPanel LCD;
        IMyCameraBlock cam;
        Queue<Vector3D> spots = new Queue<Vector3D>();
        List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            LCD = GridTerminalSystem.GetBlockWithName("LCD") as IMyTextPanel;
            cam = GridTerminalSystem.GetBlockWithName("Camera") as IMyCameraBlock;
            cam.EnableRaycast = true;
            LCD.WriteText("", false);

            IGC.RegisterBroadcastListener("Ch2");
            IGC.GetBroadcastListeners(listeners);

        }

        public void Main(string args, UpdateType updateSource)
        {
            LCD.WriteText($"");
            foreach (var spot in spots)
            {
                LCD.WriteText($"{spot}\n", true);
            }

            if (args == "stop")
            {
                var msg = $"stop";
                IGC.SendBroadcastMessage("Ch1", $"{msg}", TransmissionDistance.AntennaRelay);
            }

            if(args == "idle")
            {
                var msg = $"idle";
                IGC.SendBroadcastMessage("Ch1", $"{msg}", TransmissionDistance.AntennaRelay);
            }

            if(args == "cast")
            {
                var info = cam.Raycast(10000, 0, 0);
                if (!info.IsEmpty())
                {

                    if (info.Type == MyDetectedEntityType.Planet)
                    {
                        spots.Enqueue(info.HitPosition.Value);
                        LCD.WriteText($"Cast Success: {spots.Count}\n", true);
                    }

                }
            }

            if (listeners.Any() && listeners.FirstOrDefault().HasPendingMessage)
            {
                MyIGCMessage message = new MyIGCMessage();
                message = listeners[0].AcceptMessage();
                string messagetext = message.Data.ToString();
                //string messagetag = message.Tag;
                //long sender = message.Source;

                var msg = messagetext.Split(';');
                var cmd = msg[1];

                //LCD.WriteText($"Request: {messagetext}\n", true);

                if (cmd == "request")
                {
                    string response = string.Empty;
                    string botName = msg[0];
                    if (spots.Any())
                    {
                        var spot = spots.Dequeue();
                        response = $"{botName};{spot.X};{spot.Y};{spot.Z}";
                    }

                    IGC.SendBroadcastMessage("Ch1", $"{response}", TransmissionDistance.AntennaRelay);
                    //LCD.WriteText($"Response: {response}\n", true);
                }
            }

        }

    }
}
