using Sandbox.ModAPI.Ingame;
using System.Diagnostics;
using VRage.Game.ModAPI.Ingame;
using System.Text;

namespace SpaceEngineersInventoryComponents
{
    public sealed class Program : MyGridProgram
    {
        IMyTextPanel LCDingots;
        IMyTextPanel LCDcomps;
        IMyTextPanel LCDlack;
        int Tick = 0;
        Dictionary<string, int> ingots;
        Dictionary<string, int> comps;
        Dictionary<string, int> ores;

        string compsText;
        string ingotsText;
        string lackText;

        public Program()
        {
            ingots = new Dictionary<string, int>();
            comps = new Dictionary<string, int>();
            ores = new Dictionary<string, int>();
            LCDingots = GridTerminalSystem.GetBlockWithName("Ingots") as IMyTextPanel;
            LCDcomps = GridTerminalSystem.GetBlockWithName("Components") as IMyTextPanel;
            LCDlack = GridTerminalSystem.GetBlockWithName("Lack") as IMyTextPanel;
            

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if(Tick++ % 5 == 0) 
            {
                GetTexts();
                LCDcomps.WriteText(compsText);
                LCDlack.WriteText(lackText);
                LCDingots.WriteText(ingotsText);
            }
        }

        private void GetTexts()
        {
            comps.Clear();
            ingots.Clear();
            ores.Clear();

            int amount = 0;
            IMyInventory inv;
            List<MyInventoryItem> items;
            MyInventoryItem item;
            StringBuilder sb = new StringBuilder();
            string panelText = "";

            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(blocks);

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].HasInventory)
                {
                    amount = blocks[i].InventoryCount;
                    for (int j = 0; j < blocks[i].InventoryCount; j++)
                    {
                        inv = blocks[i].GetInventory(j);
                        items = new List<MyInventoryItem>();
                        inv.GetItems(items);
                        for (int k = 0; k < items.Count; k++)
                        {
                            item = items[k];
                            var s = item.ToString();

                            amount = (int)item.Amount;
                            if (s.Contains("Component/"))
                            {
                                var key = s.Substring(s.IndexOf('/') + 1); 
                                if (!comps.ContainsKey(key))
                                {
                                    comps.Add(key, 0);
                                }
                                comps[key] += amount;
                            }

                            if (s.Contains("Ingot/"))
                            {
                                var key = s.Substring(s.IndexOf('/') + 1);
                                if (!ingots.ContainsKey(key))
                                {
                                    ingots.Add(key, 0);
                                }
                                ingots[key] += amount;
                            }

                            if (s.Contains("Ore/"))
                            {
                                var key = s.Substring(s.IndexOf('/') + 1);
                                if (!ores.ContainsKey(key))
                                {
                                    ores.Add(key, 0);
                                }
                                ores[key] += amount;
                            }
                        }
                    }
                }
            }

            sb.Clear();
            foreach (var comp in comps.OrderBy(x => x.Key).Where(x=>x.Value > 100))
            {
                sb.Append($"{comp.Key.Substring(comp.Key.IndexOf('/')+1)}: {comp.Value}\n");
            }
            compsText = sb.ToString();

            sb.Clear();
            foreach (var ingot in ingots.OrderBy(x => x.Key))
            {
                sb.Append($"{ingot.Key.Substring(ingot.Key.IndexOf('/') + 1)}: {ingot.Value}\n");
            }
            ingotsText = sb.ToString();

            sb.Clear();
            sb.Append("Components:\n");
            foreach (var comp in comps.OrderBy(x => x.Key).Where(x => x.Value <= 100))
            {
                sb.Append($"{comp.Key.Substring(comp.Key.IndexOf('/') + 1)}: {comp.Value}\n");
            }
            sb.Append("\nIngots:\n");
            foreach (var ingot in ingots.OrderBy(x => x.Key).Where(x => x.Value <= 10000))
            {
                sb.Append($"{ingot.Key.Substring(ingot.Key.IndexOf('/') + 1)}: {ingot.Value}\n");
            }
            sb.Append("\nOres:\n");
            foreach (var ore in ores.OrderBy(x => x.Key))
            {
                sb.Append($"{ore.Key.Substring(ore.Key.IndexOf('/') + 1)}: {ore.Value}\n");
            }
            lackText = sb.ToString();

        }

    }
}
