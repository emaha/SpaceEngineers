

using Sandbox.ModAPI.Ingame;
using System.Text;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersSentisCasino
{
    public sealed class Program : MyGridProgram
    {
        int globaTick = 1;

        Random random = new Random();
        IMyTextPanel LCDOutput;
        IMyTextPanel DepositDataBase;
        IMyTextPanel LcdDepositRoom;
        IMyTextPanel LcdWithdrawRoom;

        IMySensorBlock WithdrawRoomSensor;
        IMySensorBlock DepositRoomSensor;
        IMySensorBlock BetSensor1;
        IMySensorBlock BetSensor2;
        IMySensorBlock BetSensor3;

        IMyCargoContainer ContainerWithdraw;
        IMyCargoContainer ContainerGift;
        IMyCargoContainer ContainerDeposit;
        IMyCargoContainer ContainerVault;

        const string sentiumName = "SP_Gift";
        const int baseBetAmount = 10;
        const int giftMultiplier = 10; // Мультипликатор выдавания подарочков
        const int winMultiplier = 2;

        int winnerNumber = 0;
        int rouletteTickCounter = 30;
        RouletteState rouletteState = RouletteState.Info;

        enum RouletteState { Info, Roll}

        Dictionary<string, int> Deposits = new Dictionary<string, int>();
        Dictionary<string, int> WastedMoney = new Dictionary<string, int>();
        Dictionary<string, List<Bet>> Bets = new Dictionary<string, List<Bet>>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            LCDOutput = GridTerminalSystem.GetBlockWithName("LcdOutput") as IMyTextPanel;
            DepositDataBase = GridTerminalSystem.GetBlockWithName("DepositDataBase") as IMyTextPanel;
            LcdDepositRoom = GridTerminalSystem.GetBlockWithName("LcdDepositRoom") as IMyTextPanel;
            LcdWithdrawRoom = GridTerminalSystem.GetBlockWithName("LcdWithdrawRoom") as IMyTextPanel;

            ContainerGift = GridTerminalSystem.GetBlockWithName("ContainerRandomGift") as IMyCargoContainer;
            ContainerDeposit = GridTerminalSystem.GetBlockWithName("ContainerDeposit") as IMyCargoContainer;
            ContainerVault = GridTerminalSystem.GetBlockWithName("ContainerVault") as IMyCargoContainer;
            ContainerWithdraw = GridTerminalSystem.GetBlockWithName("ContainerWithdraw") as IMyCargoContainer;

            WithdrawRoomSensor = GridTerminalSystem.GetBlockWithName("SensorWithdrawRoom") as IMySensorBlock;
            DepositRoomSensor = GridTerminalSystem.GetBlockWithName("SensorDepositRoom") as IMySensorBlock;
            BetSensor1 = GridTerminalSystem.GetBlockWithName("SensorBet1") as IMySensorBlock;
            BetSensor2 = GridTerminalSystem.GetBlockWithName("SensorBet2") as IMySensorBlock;
            BetSensor3 = GridTerminalSystem.GetBlockWithName("SensorBet3") as IMySensorBlock;

            LoadDB();
        }

        public void Main(string argument)
        {
            switch (argument)
            {
                case "bet1":
                    MakeBetCommon(1);
                    return;

                case "bet2":
                    MakeBetCommon(2);
                    return;

                case "bet3":
                    MakeBetCommon(3);
                    return;
                case "withdraw":
                    Withdraw();
                    return;
            }

            CheckWithdrawRoom();
            CheckDepositRoom();
            ManageRoulette();

            if (globaTick++ % 60 == 0)
            {
                SaveDB();
            }

        }

        private void MakeBetCommon(int number)
        {
            var entities = new List<MyDetectedEntityInfo>();
            var sensor = GetProperSensor(number);

            sensor.DetectedEntities(entities);
            if (!entities.Any())
            {
                return;
            }
            var playerId = entities[0].EntityId.ToString();
            if (!IsEnoughMoney(playerId, baseBetAmount)) return;

            MakePlayerBet(playerId, new Bet { Number = number, Amount = baseBetAmount });            
        }

        private bool IsEnoughMoney(string playerId, int amount)
        {
            int money;
            if (Deposits.TryGetValue(playerId, out money))
            {
                return money >= amount;
            }
            return false;
        }

        private void MakePlayerBet(string playerId, Bet playerBet)
        {
            List<Bet> bets;
            if (Bets.TryGetValue(playerId, out bets))
            {
                bets.Add(playerBet);
            }
            else
            {
                Bets.Add(playerId, new List<Bet>() { playerBet });
            }
            AddMoney(playerId, -baseBetAmount);
        }

        private IMySensorBlock GetProperSensor(int index)
        {
            if (index == 1) return BetSensor1;
            if (index == 2) return BetSensor2;
            if (index == 3) return BetSensor3;
            return null;
        }

        private void Roll()
        {
            winnerNumber = random.Next(1, 4);
            rouletteTickCounter = 30;

            foreach (var item in Bets)
            {
                var playerId = item.Key;
                var playerBets = item.Value;
                foreach(var bet in playerBets)
                {
                    if(bet.Number == winnerNumber)
                    {
                        AddMoney(playerId, bet.Amount * winMultiplier);
                    }
                    else
                    {
                        AddWasted(playerId, bet.Amount);
                    }
                }

            }

            Bets.Clear();
        }

        private void Withdraw()
        {
            var entities = new List<MyDetectedEntityInfo>();
            WithdrawRoomSensor.DetectedEntities(entities);
            if (!entities.Any())
            {
                return;
            }
            var playerId = entities[0].EntityId.ToString();
            var depositAmount = GetPlayerDeposit(playerId); //amount of sentium
            var playerWastedMoney = GetPlayerWastedMoney(playerId);
            var giftAmount = playerWastedMoney * giftMultiplier;

            List<MyInventoryItem> items = new List<MyInventoryItem>();
            var withdrawInventory = ContainerWithdraw.GetInventory(0);
            withdrawInventory.GetItems(items);

            var vaultInventory = ContainerVault.GetInventory(0);
            var randomGiftInventory = ContainerGift.GetInventory(0);

            var sentiumItem = FindItem(vaultInventory, sentiumName);
            if (!sentiumItem.HasValue) return;

            var giftItem = FindRandomItem(randomGiftInventory);
            if (giftItem.HasValue)
            {
                Echo($"1");
                randomGiftInventory.TransferItemTo(withdrawInventory, giftItem.Value, Math.Min(giftAmount, giftItem.Value.Amount.ToIntSafe()));
            }
            else
            {
                Echo($"2");
            }

            vaultInventory.TransferItemTo(withdrawInventory, sentiumItem.Value, depositAmount);
            AddMoney(playerId, -depositAmount);
            AddWasted(playerId, -playerWastedMoney);
        }

        private int GetPlayerWastedMoney(string playerId)
        {
            int money;
            WastedMoney.TryGetValue(playerId, out money);
            return money;
        }

        private void LoadDB()
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

        private void SaveDB()
        {
            string text = "";
            foreach (var item in Deposits)
            {
                text += $"{item.Key};{item.Value};";
            }
            
            DepositDataBase.WriteText(text.TrimEnd(';'));
        }

        private void CheckDepositRoom()
        {
            string depositeLcdText;

            List<MyInventoryItem> items = new List<MyInventoryItem>();
            var depositInventory = ContainerDeposit.GetInventory(0);
            depositInventory.GetItems(items);

            var entities = new List<MyDetectedEntityInfo>();
            DepositRoomSensor.DetectedEntities(entities);
            if (!entities.Any())
            {
                depositeLcdText = "Deposit room";
                LcdDepositRoom.WriteText(depositeLcdText);
                return;
            }
            
            var playerId = entities[0].EntityId.ToString();

            depositeLcdText = $"\nHello\n\n" +
                $"Your deposit is\n" +
                $"{GetPlayerDeposit(playerId)}";

            LcdDepositRoom.WriteText(depositeLcdText);

            var sentiumItem = FindItem(depositInventory, sentiumName);
            if (!sentiumItem.HasValue) return;

            var vaultInventory = ContainerVault.GetInventory(0);
            vaultInventory.TransferItemFrom(depositInventory, sentiumItem.Value);
            AddMoney(playerId, sentiumItem.Value.Amount.ToIntSafe());
        }

        private void CheckWithdrawRoom()
        {
            string withdrawLcdText;

            var entities = new List<MyDetectedEntityInfo>();
            WithdrawRoomSensor.DetectedEntities(entities);
            if (!entities.Any())
            {
                withdrawLcdText = "Withdraw Room";
                LcdWithdrawRoom.WriteText(withdrawLcdText);
                return;
            }

            var playerId = entities[0].EntityId.ToString();

            withdrawLcdText = $"\nHello\n\n" +
                $"Your deposit is\n" +
                $"{GetPlayerDeposit(playerId)} ({GetPlayerWastedMoney(playerId)})";

            LcdWithdrawRoom.WriteText(withdrawLcdText);
        }

        private int GetPlayerDeposit(string playerId)
        {
            int money;
            Deposits.TryGetValue(playerId, out money);
            return money;
        }

        private void AddWasted(string playerId, int amount)
        {
            int curAmount;
            if (WastedMoney.TryGetValue(playerId, out curAmount))
            {
                WastedMoney[playerId] = curAmount + amount;
            }
            else
            {
                WastedMoney.Add(playerId, amount);
            }
        }

        private void AddMoney(string playerId, int amount)
        {
            int curAmount;
            if (Deposits.TryGetValue(playerId, out curAmount))
            {
                Deposits[playerId] = curAmount + amount;
            }
            else
            {
                Deposits.Add(playerId, amount);
            }
        }

        private void ManageRoulette()
        {
            switch (rouletteState)
            {
                case RouletteState.Info:
                    if(--rouletteTickCounter == 0)
                    {
                        rouletteState=RouletteState.Roll;
                    }
                    int totalBets = CalcTotalBets();
                    var rouletteText = $"Number [{winnerNumber}] WIN\n\n" +
                        $"Next roll in {rouletteTickCounter} ticks\n\n" +
                        $"Total bets: {totalBets}";

                    LCDOutput.WriteText(rouletteText);
                    break;
                case RouletteState.Roll:
                    Roll();
                    rouletteState = RouletteState.Info;
                    break;
                default:
                    break;
            }

        }

        private int CalcTotalBets()
        {
            int sum = 0;
            foreach (var item in Bets)
            {
                var playerId = item.Key;
                var playerBets = item.Value;
                foreach (var bet in playerBets)
                {
                    sum += bet.Amount;
                }

            }

            return sum;
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

        private MyInventoryItem? FindRandomItem(IMyInventory inventory)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inventory.GetItems(items);
            int index = random.Next(0, items.Count);
            Echo($"Rnd:{items.Count}");

            return items[index];
        }

        struct Bet
        {
            public int Number;
            public int Amount;
        }

    }
}
