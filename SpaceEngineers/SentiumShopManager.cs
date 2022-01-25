

using Sandbox.ModAPI.Ingame;
using System.Text;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersSentiumShopManager
{
    // Скрипт для магаза 
    public sealed class Program : MyGridProgram
    {
        IMyTextPanel LCDInput;
        IMyTextPanel LCDOutput;
        IMyTextPanel LCDController;
        IMyTextPanel LCDError;
        IMyTextPanel LCDTradelog;

        IMyCargoContainer Input;
        IMyCargoContainer Output;
        IMyCargoContainer Vault;

        IMyInventory inputInventory;
        IMyInventory outputInventory;
        IMyInventory vaultInventory;

        IMyTerminalBlock mySoundBlock;
        IMySensorBlock sensor;

        string errorText = string.Empty;
        string tradeLog = string.Empty;
        const string sentiumName = "SP_Gift";
        int currentIndex = 0;

        const int T1price = 150;
        const int T2price = 50;
        const int T3price = 5;

        int tick = 0;

        Dictionary<string, int> prices = new Dictionary<string, int>
        {
            // T3
            //{"Gee", T3price },
            //{"Irr", T3price },
            //{"Tii", T3price },
            //{"Znn", T3price },

            // T2
            {"Aluminum", T2price },
            {"Copper", T2price },
            {"Scandium", T2price },
            {"Molybdenum", T2price },
            {"Platinum", T2price },

            // Vanilla
            {"Uranium", T2price },

            {"Iron", T1price },
            {"Gold", T1price },
            {"Cobalt", T1price },
            {"Silicon", T1price },
            {"Magnesium", T1price },
            {"Nickel", T1price },
            {"Silver", T1price },
        };
        Dictionary<string, string> locale = new Dictionary<string, string>
        {
            // T3
            //{"Gee", "Германий" },
            //{"Irr", "Иридий" },
            //{"Tii", "Титан" },
            //{"Znn", "Цинк" },

            // T2
            {"Aluminum", "Алюминий" },
            {"Copper", "Медь" },
            {"Scandium", "Скандий"},
            {"Molybdenum", "Молибден"},
            {"Platinum", "Платина" },

            // Vanilla
            {"Uranium", "Уран" },
            {"Iron", "Железо" },
            {"Gold", "Золото" },
            {"Cobalt", "Кобальт" },
            {"Silicon", "Кремний" },
            {"Magnesium", "Магний"},
            {"Nickel", "Никель"},
            {"Silver", "Серебро" }
        };

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            mySoundBlock = GridTerminalSystem.GetBlockWithName("Alarm") as IMyTerminalBlock;

            LCDInput = GridTerminalSystem.GetBlockWithName("LcdInput") as IMyTextPanel;
            LCDOutput = GridTerminalSystem.GetBlockWithName("LcdOutput") as IMyTextPanel;
            LCDController = GridTerminalSystem.GetBlockWithName("LcdController") as IMyTextPanel;
            LCDError = GridTerminalSystem.GetBlockWithName("LcdError") as IMyTextPanel;
            LCDTradelog = GridTerminalSystem.GetBlockWithName("LcdTradelog") as IMyTextPanel;

            sensor = GridTerminalSystem.GetBlockWithName("Sensor") as IMySensorBlock;

            tradeLog = LCDTradelog.GetText();
            if (string.IsNullOrEmpty(tradeLog)) tradeLog = "Trade log:\n";

            List <IMyTerminalBlock> inOutContainerList = new List<IMyTerminalBlock>();
            var containers = GridTerminalSystem.GetBlockGroupWithName("InOut");
            containers.GetBlocks(inOutContainerList);
            var first = inOutContainerList.First() as IMyCargoContainer;
            var last = inOutContainerList.Last() as IMyCargoContainer;

            if(first.GetInventory().MaxVolume > last.GetInventory().MaxVolume)
            {
                Input = last;
                Output = first;
            }
            else
            {
                Input = first;
                Output = last;
            }

            //Input = GridTerminalSystem.GetBlockWithName("ContainerInput") as IMyCargoContainer;
            //Output = GridTerminalSystem.GetBlockWithName("ContainerOutput") as IMyCargoContainer;
            Vault = GridTerminalSystem.GetBlockWithName("ContainerVault") as IMyCargoContainer;

            Echo($"Containers ready: {Input != null && Output != null && Vault != null}");

            inputInventory = Input.GetInventory(0);
            outputInventory = Output.GetInventory(0);
            vaultInventory = Vault.GetInventory(0);
        }

        public void Main(string argument)
        {
            switch (argument)
            {
                case "clearLog":
                    tradeLog = string.Empty;
                    LCDTradelog.WriteText(tradeLog);
                    break;
                case "trade":
                    Trade();
                    break;

                case "up":
                    errorText = "";
                    if(currentIndex>0) currentIndex--;
                    ShowInputInfo();
                    ShowMenu();
                    break;

                case "down":
                    errorText = "";
                    if (currentIndex < prices.Count-1) currentIndex++;
                    ShowInputInfo();
                    ShowMenu();
                    break;
            }

            ShowErrorLog();
            ShowInputInfo();
            ShowMenu();
        }

        private void ShowMenu()
        {
            LCDController.WriteText(GetMenuText(currentIndex));
        }

        private void ShowErrorLog()
        {
            if(errorText != string.Empty)
            {
                LCDError.WriteText($"Ошибки:\n\n\n{errorText}");
            }
            else
            {
                LCDError.WriteText($"\n\nДля совершения обмена\n\n" +
                    $"------------------>\n\n" +
                    $"выбери пункт в меню ниже\n" +
                    $"|\n|\n|\n" +
                    $"\\/");
            }
        }

        private void ShowInputInfo()
        {
            var sentium = FindItem(inputInventory, sentiumName);

            var inText = $"\n\n\nВы отдаете:\n" +
                $"Сентиум x {(sentium.HasValue ? sentium.Value.Amount : 0)}\n";
            var outText = $"\n\n\nВы получите:\n" +
                $"{GetLocalIngotName(GetCurrentIngotName())} x {(sentium.HasValue ? sentium.Value.Amount * GetCurrentIngotRatio() : 0)}";

            LCDInput.WriteText($"{inText}");
            LCDOutput.WriteText($"{outText}");

            if(tick++ > 1)  mySoundBlock.GetActionWithName("StopSound").Apply(mySoundBlock);
        }

        private void Trade()
        {
            List<MyInventoryItem> inputItems = new List<MyInventoryItem>();
            inputInventory.GetItems(inputItems);

            errorText = string.Empty;

            foreach (var it in inputItems)
            {
                var amount = it.Amount;

                var inputItem = FindItem(inputInventory, sentiumName);
                var returnItem = FindItem(vaultInventory, GetCurrentIngotName());
                if(inputItem.HasValue)
                {
                    VRage.MyFixedPoint outputVolume = inputItem.Value.Amount * GetCurrentIngotRatio();

                    if (!returnItem.HasValue || returnItem.Value.Amount < outputVolume.ToIntSafe())
                    {
                        errorText += "Извините.\n" +
                            "Не достаточно\n" +
                            "ресурсов для обмена\n" +
                            "на станции.\n\n" +
                            "Попробуйте выбрать\n" +
                            "меньшее количество.";
                        Alarm();
                        return;
                    }

                    if(!CheckIfEnoughSpaceInOutput(returnItem.Value, outputVolume))
                    {
                        Alarm();
                        return;
                    }

                    inputInventory.TransferItemTo(vaultInventory, inputItem.Value); 
                    outputInventory.TransferItemFrom(vaultInventory, returnItem.Value, outputVolume);

                    var entities = new List<MyDetectedEntityInfo>();
                    sensor.DetectedEntities(entities);
                    
                    tradeLog += $"{DateTime.Now.ToString("yy.MM.dd-HH:mm")} : {amount} {sentiumName} -> " +
                        $"{GetCurrentIngotName()} {outputVolume} [{entities.FirstOrDefault().EntityId}]\n";
                    LCDTradelog.WriteText(tradeLog);

                    MyInventoryItem aaa; 
                }
                
            }
            
        }

        private void Alarm()
        {
            tick = 0;
            mySoundBlock.GetActionWithName("PlaySound").Apply(mySoundBlock);
        }

        private MyInventoryItem? FindItem(IMyInventory inventory, string ingotName)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inventory.GetItems(items);
            foreach (var item in items)
            {
                if (item.ToString().Contains(ingotName))
                {
                    return item;
                }
            }
            return null;
        }

        private string FormatItem(MyInventoryItem item)
        {
            var name = item.ToString();
            return $"{name.ToString().Substring(name.IndexOf('/') + 1)}";
        }

        private string GetMenuText(int index)
        {
            var sb = new StringBuilder();
            for(int i = 0; i < prices.Count;i++)
            {
                if(i == index)
                {
                    sb.Append($">>>>> {GetLocalIngotName(prices.ElementAt(i).Key)} 1:{prices.ElementAt(i).Value} <<<<<\n");
                }
                else
                {
                    sb.Append($"            {GetLocalIngotName(prices.ElementAt(i).Key)} 1:{prices.ElementAt(i).Value}\n");
                }
                
            }

            return sb.ToString();
        }

        private bool CheckIfEnoughSpaceInOutput(MyInventoryItem item, VRage.MyFixedPoint amount)
        {
            var typeVolume = item.Type.GetItemInfo().Volume;
            var volume = typeVolume * amount.ToIntSafe() * 1000;
            var freeVolume = (outputInventory.MaxVolume - outputInventory.CurrentVolume) * 1000;

            var isEnough = freeVolume.ToIntSafe() >= volume;
            if (!isEnough)
            {
                errorText += "Не достаточно места \nв выходном контейнере\n\n" +
                    $"Нужно: {volume}л\n\n" +
                    $"Свободно: {freeVolume.ToIntSafe()}л\n";
            }

            return isEnough;
        }

        private string GetCurrentIngotName()
        {
            return prices.ElementAt(currentIndex).Key;
        }

        private int GetCurrentIngotRatio()
        {
            return prices.ElementAt(currentIndex).Value;
        }

        private string GetLocalIngotName(string name)
        {
            return locale[name];
        }

    }
}
