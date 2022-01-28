using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersSentisDataCenter
{
    public sealed class Program : MyGridProgram
    {
        IMyTextPanel DepositDataBase;

        int Tick = 1;
        Dictionary<string, int> Deposits = new Dictionary<string, int>();
        List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            IGC.RegisterBroadcastListener("Ch1");
            IGC.GetBroadcastListeners(listeners);

            //LCD Panels
            DepositDataBase = GridTerminalSystem.GetBlockWithName("DepositDataBase") as IMyTextPanel;

            LoadBalancesFromDataBase();
        }

        public void Main(string argument)
        {
            switch (argument)
            {
                case "Init":
                    break;
            }

            ManageRadio();
            Tick++;
        }

        private void ManageRadio()
        {
            if (listeners.Any() && listeners.FirstOrDefault().HasPendingMessage)
            {
                MyIGCMessage message = listeners[0].AcceptMessage();
                string messagetext = message.Data.ToString();

                var msg = messagetext.Split(';');
                var cmd = msg.FirstOrDefault();

                switch (cmd)
                {
                    case "getbalances":
                        var balances = GetPlayerBalance("");
                        var response = "";
                        IGC.SendBroadcastMessage("Ch2", $"{response}", TransmissionDistance.AntennaRelay);
                        break;
                    case "setbalances":

                        break;

                }
            }
        }

        private void LoadBalancesFromDataBase()
        {
            var data = DepositDataBase.GetText();
            if (string.IsNullOrEmpty(data))
            {
                Echo("Empty database");
                return;
            }

            var rawData = data.Split(';');

            for (var i = 0; i<rawData.Count(); i++)
            {
                Deposits.Add(rawData[i], Convert.ToInt32(rawData[i + 1]));
                i++;
            }

            Echo($"DataBase loaded: count={Deposits.Count}");
        }

        private void SaveBalancesToDataBase()
        {
            string text = "";
            foreach (var item in Deposits)
            {
                text += $"{item.Key};{item.Value};";
            }
            
            DepositDataBase.WriteText(text.TrimEnd(';'));
        }

        private int GetPlayerBalance(string playerId)
        {
            int money;
            Deposits.TryGetValue(playerId, out money);
            return money;
        }

    }
}
