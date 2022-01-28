using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersSentisCasino
{
    public sealed class Program : MyGridProgram
    {
        Bank bank;
        Roulette roulette;
        BlackJack blackjack;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            bank = new Bank(GridTerminalSystem);
            roulette = new Roulette(GridTerminalSystem, bank);
            blackjack = new BlackJack(GridTerminalSystem, bank);

            bank.LoadBalancesFromDataBase();
        }

        public void Main(string argument)
        {
            if (argument.Contains("rouletteBet"))
                roulette.HandleCommand(argument);

            if (argument.Contains("blackjack"))
                blackjack.HandleCommand(argument);

            switch (argument)
            {
                case "withdraw":
                    bank.Withdraw();
                    return;
            }

            bank.CheckWithdrawRoom();
            bank.CheckDepositRoom();
            bank.SaveBalancesToDataBase();

            roulette.Update();
            blackjack.Update();
        }

        public class Bank
        {
            private readonly IMyGridTerminalSystem _system;

            private const string sentiumName = "SP_Gift";
            private Random random = new Random();

            private IMyTextPanel DepositDataBase;
            private IMyTextPanel LcdDepositRoom;
            private IMyTextPanel LcdWithdrawRoom;

            private IMySensorBlock WithdrawRoomSensor;
            private IMySensorBlock DepositRoomSensor;

            private IMyCargoContainer ContainerWithdraw;
            private IMyCargoContainer ContainerGift;
            private IMyCargoContainer ContainerDeposit;
            private IMyCargoContainer ContainerVault;

            private const int giftMultiplier = 2; // Мультипликатор выдавания подарочков

            private Dictionary<string, int> Deposits = new Dictionary<string, int>();
            private Dictionary<string, int> WastedMoney = new Dictionary<string, int>();

            public Bank(IMyGridTerminalSystem system)
            {
                _system = system;
                Init(); 
            }

            public void Init() 
            {
                //LCD Panels
                DepositDataBase = _system.GetBlockWithName("DepositDataBase") as IMyTextPanel;
                LcdDepositRoom = _system.GetBlockWithName("LcdDepositRoom") as IMyTextPanel;
                LcdWithdrawRoom = _system.GetBlockWithName("LcdWithdrawRoom") as IMyTextPanel;

                //Containers
                ContainerGift = _system.GetBlockWithName("ContainerRandomGift") as IMyCargoContainer;
                ContainerDeposit = _system.GetBlockWithName("ContainerDeposit") as IMyCargoContainer;
                ContainerVault = _system.GetBlockWithName("ContainerVault") as IMyCargoContainer;
                ContainerWithdraw = _system.GetBlockWithName("ContainerWithdraw") as IMyCargoContainer;

                //Sensors
                WithdrawRoomSensor = _system.GetBlockWithName("SensorWithdrawRoom") as IMySensorBlock;
                DepositRoomSensor = _system.GetBlockWithName("SensorDepositRoom") as IMySensorBlock;
            }

            private int GetPlayerBalance(string playerId)
            {
                int money;
                Deposits.TryGetValue(playerId, out money);
                return money;
            }

            public void AddMoney(string playerId, int amount)
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

            public void LoadBalancesFromDataBase()
            {
                var data = DepositDataBase.GetText();
                if (string.IsNullOrEmpty(data)) return;
                
                var rawData = data.Split('\n');
                for (var i = 0; i < rawData.Count(); i++)
                {
                    var line = rawData[i].Split(';');
                    Deposits.Add(line[0], Convert.ToInt32(line[1]));
                }
            }

            public void CheckDepositRoom()
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
                    $"Your balance is\n" +
                    $"{GetPlayerBalance(playerId)}";

                LcdDepositRoom.WriteText(depositeLcdText);

                var sentiumItem = FindItem(depositInventory, sentiumName);
                if (!sentiumItem.HasValue) return;

                var vaultInventory = ContainerVault.GetInventory(0);
                vaultInventory.TransferItemFrom(depositInventory, sentiumItem.Value);
                AddMoney(playerId, sentiumItem.Value.Amount.ToIntSafe());
            }

            public void CheckWithdrawRoom()
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

                withdrawLcdText = $"Hello\n\n" +
                    $"Your balance is\n" +
                    $"{GetPlayerBalance(playerId)}\n" +
                    $"bonus ({GetPlayerWastedMoney(playerId)})\n\n" +
                    $"Thanks for playing";

                LcdWithdrawRoom.WriteText(withdrawLcdText);
            }

            public void SaveBalancesToDataBase()
            {
                string text = "";
                foreach (var item in Deposits)
                {
                    text += $"{item.Key};{item.Value}\n";
                }

                DepositDataBase.WriteText(text.TrimEnd('\n'));
            }

            public void Withdraw()
            {
                var entities = new List<MyDetectedEntityInfo>();
                WithdrawRoomSensor.DetectedEntities(entities);
                if (!entities.Any())
                {
                    return;
                }
                var playerId = entities[0].EntityId.ToString();
                var sentiumAmountToWithdraw = GetPlayerBalance(playerId); //amount of sentium
                var playerWastedMoney = GetPlayerWastedMoney(playerId);
                var giftAmount = playerWastedMoney * giftMultiplier;

                List<MyInventoryItem> items = new List<MyInventoryItem>();
                var withdrawInventory = ContainerWithdraw.GetInventory(0);
                withdrawInventory.GetItems(items);

                var vaultInventory = ContainerVault.GetInventory(0);
                var randomGiftInventory = ContainerGift.GetInventory(0);

                var sentiumItem = FindItem(vaultInventory, sentiumName);
                if (!sentiumItem.HasValue || sentiumItem.Value.Amount.ToIntSafe() < sentiumAmountToWithdraw) return;

                var giftItem = FindRandomItem(randomGiftInventory);
                if (giftItem.HasValue)
                {
                    randomGiftInventory.TransferItemTo(withdrawInventory, giftItem.Value, Math.Min(giftAmount, giftItem.Value.Amount.ToIntSafe()));
                }

                vaultInventory.TransferItemTo(withdrawInventory, sentiumItem.Value, sentiumAmountToWithdraw);
                AddMoney(playerId, -sentiumAmountToWithdraw);
                AddWastedMoney(playerId, -playerWastedMoney);
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

                return items[index];
            }

            public bool IsEnoughMoney(string playerId, int amount)
            {
                int money;
                if (Deposits.TryGetValue(playerId, out money))
                {
                    return money >= amount;
                }
                return false;
            }

            public int GetPlayerWastedMoney(string playerId)
            {
                int money;
                WastedMoney.TryGetValue(playerId, out money);
                return money;
            }

            public void AddWastedMoney(string playerId, int amount)
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
        }
        public class Roulette
        {
            private readonly IMyGridTerminalSystem _system;
            private readonly Bank _bank;

            private IMyTextPanel LcdRouletteMainPanel;
            private IMyTextPanel LcdBetTotal1;
            private IMyTextPanel LcdBetTotal2;
            private IMyTextPanel LcdBetTotal3;
            
            private IMySensorBlock BetSensor1;
            private IMySensorBlock BetSensor2;
            private IMySensorBlock BetSensor3;

            private const int winCommision = 5; // 5% comission

            private int rouletteWinnerNumber = 0;
            private int rouletteTickCounter = 30;

            private Dictionary<string, List<Bet>> Bets = new Dictionary<string, List<Bet>>();
            private RouletteState rouletteState = RouletteState.Info;

            Random random = new Random();

            enum RouletteState { Info, Roll }
            struct Bet
            {
                public int Number;
                public int Amount;
            }

            public Roulette(IMyGridTerminalSystem system, Bank bank)
            {
                _system = system;
                _bank = bank;

                Init();
            }

            public void Init()
            {
                //LCDs
                LcdRouletteMainPanel = _system.GetBlockWithName("LcdRouletteMainPanel") as IMyTextPanel;
                LcdBetTotal1 = _system.GetBlockWithName("LcdBetTotal1") as IMyTextPanel;
                LcdBetTotal2 = _system.GetBlockWithName("LcdBetTotal2") as IMyTextPanel;
                LcdBetTotal3 = _system.GetBlockWithName("LcdBetTotal3") as IMyTextPanel;

                // Sensors
                BetSensor1 = _system.GetBlockWithName("SensorBet1") as IMySensorBlock;
                BetSensor2 = _system.GetBlockWithName("SensorBet2") as IMySensorBlock;
                BetSensor3 = _system.GetBlockWithName("SensorBet3") as IMySensorBlock;
            }

            public void HandleCommand(string command)
            {
                var args = command.Split(';');
                int number = Convert.ToInt32(args[1]);
                int amount = Convert.ToInt32(args[2]);

                var entities = new List<MyDetectedEntityInfo>();
                var sensor = GetProperSensor(number);

                sensor.DetectedEntities(entities);
                if (!entities.Any())
                {
                    return;
                }
                var playerId = entities[0].EntityId.ToString();
                if (!_bank.IsEnoughMoney(playerId, amount)) return;

                MakePlayerBet(playerId, new Bet { Number = number, Amount = amount });
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
                _bank.AddMoney(playerId, -playerBet.Amount);
            }

            private IMySensorBlock GetProperSensor(int index)
            {
                if (index == 1) return BetSensor1;
                if (index == 2) return BetSensor2;
                if (index == 3) return BetSensor3;
                return null;
            }

            private void RollRoulette()
            {
                rouletteWinnerNumber = random.Next(1, 4);
                rouletteTickCounter = 30;

                var playerPercentager = CalcWinnersDepositPercenatge(rouletteWinnerNumber);
                int total = (int)(CalcTotalBets() / 100.0f * (100 - winCommision));

                foreach(var pp in playerPercentager)
                {
                    _bank.AddMoney(pp.Key, pp.Value * total);
                }

                // Calc wasted money (for gift)
                foreach (var item in Bets)
                {
                    var playerId = item.Key;
                    var playerBets = item.Value;
                    foreach (var bet in playerBets)
                    {
                        if (bet.Number != rouletteWinnerNumber) _bank.AddWastedMoney(playerId, bet.Amount);
                    }

                }

                Bets.Clear();
            }

            public void Update()
            {
                switch (rouletteState)
                {
                    case RouletteState.Info:
                        if (--rouletteTickCounter == 0)
                            rouletteState = RouletteState.Roll;
                        ShowBetsInfo();
                        break;

                    case RouletteState.Roll:
                        RollRoulette();
                        rouletteState = RouletteState.Info;
                        break;

                    default:
                        break;
                }

            }

            private void ShowBetsInfo()
            {
                int totalBets = CalcTotalBets();
                var rouletteText = $"Number [{rouletteWinnerNumber}] WIN\n\n" +
                    $"Next roll in {rouletteTickCounter} ticks\n\n" +
                    $"Total bets: {totalBets}";

                LcdRouletteMainPanel.WriteText(rouletteText);
                LcdBetTotal1.WriteText(CalcBetSumByNumber(1).ToString());
                LcdBetTotal2.WriteText(CalcBetSumByNumber(2).ToString());
                LcdBetTotal3.WriteText(CalcBetSumByNumber(3).ToString());
            }

            private Dictionary<string,int> CalcWinnersDepositPercenatge(int number)
            {
                int sum = 0;
                var deposits = new Dictionary<string,int>();
                foreach (var item in Bets)
                {
                    var playerId = item.Key;
                    if(!deposits.ContainsKey(playerId)) deposits.Add(playerId, 0); 

                    var playerBets = item.Value;
                    foreach (var bet in playerBets.Where(x=>x.Number == number))
                    {
                        deposits[playerId] += bet.Amount;
                        sum += bet.Amount;
                    }
                }

                var percentages = new Dictionary<string,int>();
                if (sum > 0)
                {
                    foreach (var deposit in deposits)
                    {
                        percentages.Add(deposit.Key, deposit.Value / sum);
                    }
                }

                return percentages;
            }

            private int CalcBetSumByNumber(int number)
            {
                int sum = 0;
                foreach (var item in Bets)
                {
                    var playerId = item.Key;
                    var playerBets = item.Value;
                    foreach (var bet in playerBets.Where(x=>x.Number == number))
                    {
                        sum += bet.Amount;
                    }
                }
                return sum;
            }

            private int CalcTotalBets()
            {
                return CalcBetSumByNumber(1)
                    + CalcBetSumByNumber(2)
                    + CalcBetSumByNumber(3);
            }

        }
        
        public class BlackJack
        {
            private readonly IMyGridTerminalSystem _system;
            private readonly Bank _bank;

            private IMyTextPanel LcdTable1;
            private IMyTextPanel LcdTable2;

            private const int ShuffleCount = 100;
            public enum Value { THO, THREE, FOUR, FIVE, SIX, SEVEN, EIGHT, NINE, TEN, JACK, QUEEN, KING, ACE}
            public enum Suit { HEART, DIAMOND, SPADE, CLUB }
            public enum GameState { WELCOME, PLAYERTURN, DEALERTURN, RESULTS}

            Random r = new Random();
            List<int> yourHand = new List<int>();
            List<int> dealerHand = new List<int>();
            private GameState state = GameState.WELCOME;


            public BlackJack(IMyGridTerminalSystem system, Bank bank)
            {
                _system = system;
                _bank = bank;

                Init();
                
                StartNewGame();
            }

            private void Init()
            {
                LcdTable1 = _system.GetBlockWithName("LcdBlackJack1") as IMyTextPanel;
            }

            public void HandleCommand(string command)
            {
                var args = command.Split(';');
                int tableId = Convert.ToInt32(args[1]);
                int gameCommand = Convert.ToInt32(args[2]);

            }

            public void StartNewGame()
            {

                yourHand.Clear();
                dealerHand.Clear();

                HandOutCards();
            }

            private void HandOutCards()
            {
                for(int i = 0; i < 2; i++)
                {
                    int number = r.Next(1, 14); // 10, JACK, QUEEN, KING = 10
                    if (number > 10) number = 10;

                    yourHand.Add(number);
                    dealerHand.Add(number);
                }
            }

            private void ShowGameStateInfo()
            {
                var dealerText = state != GameState.DEALERTURN
                    ? $"{dealerHand[0]}      ?"
                    : GetFormatedText(dealerHand);

                var playerId = "123456789654231";
                var text = $"Player {playerId}\n\n" +
                    $"Dealer hand\n" +
                    $"{dealerText}\n\n" +
                    $"Hit or Stand?" +
                    $"\n\n" +
                    $"{GetFormatedText(yourHand)}\n" +
                    $"Your hand";

                LcdTable1.WriteText(text);
            }

            private string GetFormatedText(List<int> cards)
            {
                StringBuilder sb = new StringBuilder();
                foreach(var card in cards)
                {
                    sb.Append($"{card}      ");
                }

                return sb.ToString().TrimEnd();
            }

            public void Update()
            {
                ShowGameStateInfo();
            }


            public class Game{}


        }






















    }
}
