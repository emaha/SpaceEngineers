using Sandbox.ModAPI.Ingame;
using System.Text;

namespace SpaceEngineers
{
    public sealed class Program : MyGridProgram
    {

        IMyTextPanel LCD;
        List<IMyBatteryBlock> vBatteries = new List<IMyBatteryBlock>();
        int i = 0;


        public Program()
        {
            LCD = GridTerminalSystem.GetBlockWithName("LCD") as IMyTextPanel;
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Init();
        }

        public void Init()
        {
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(vBatteries);
        }

        public void Main(string args, UpdateType updateSource)
        {
            LCD.WriteText($"", false);
            LCD.WriteText($"Tick:{i++}\n", true);

            if (i % 10 == 0)
            {
                Init();
            }

            var battStatus = BattaryStatusText();
            LCD.WriteText($"\nBattaries:\n{battStatus}", true);
        }

        public string BattaryStatusText()
        {
            var sb = new StringBuilder();
            foreach (var batt in vBatteries)
            {
                var curPower = (int)(batt.CurrentStoredPower / batt.MaxStoredPower * 100);
                var curOut = (int)(batt.CurrentOutput / batt.MaxOutput * 100);
                var curIn = (int)(batt.CurrentInput / batt.MaxInput * 100);
                if (curIn < 0) curIn = 0;
                if (curOut < 0) curOut = 0;


                sb.AppendLine($"[ {curPower} % ] ( +{curIn} -{curOut} )");
            }

            return sb.ToString();
        }







        public void Save(){}


    }
}