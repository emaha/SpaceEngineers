

using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersLeechInventoryManager
{
    // Скрипт для пиявки, вытягивает из конекторов руди и кладет ее в контейнер. 
    public sealed class Program : MyGridProgram
    {
        List<IMyTerminalBlock> Connectors = new List<IMyTerminalBlock>();
        IMyCargoContainer Container;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            var cons = GridTerminalSystem.GetBlockGroupWithName("Connectors");
            cons.GetBlocks(Connectors);
            Echo($"Connectors: {Connectors.Count}");

            Container = GridTerminalSystem.GetBlockWithName("Heap") as IMyCargoContainer;
            Echo($"Main container found: {Container != null}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Sort();
        }

        private void Sort()
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(blocks);
            IMyInventory inv;
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            MyInventoryItem item;


            foreach (var block in Connectors)
            {
                inv = block.GetInventory(0);
                inv.GetItems(items);

                foreach (var it in items)
                {
                    inv.TransferItemTo(Container.GetInventory(0), it);
                }
            }
        }

    }
}
