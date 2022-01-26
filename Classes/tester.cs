using HiveAPI.CS;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class tester
    {
        private static Random random = new Random();


        internal static class Renter
        {

        }

        public class CardForRent
        {
            public int card_detail_id;
            public double low_price_bcx;
            public int rarity;
            public int DECValue;
            public double DECToCP;
            public string name;
            public string edition;



            public CardForRent(int card_detail_id, int rarity, double low_price_bcx, string name, string edition)
            {
                this.card_detail_id = card_detail_id;
                this.rarity = rarity;
                this.low_price_bcx = low_price_bcx;
                this.name = name;
                this.edition = edition;

                ComputeDECValue();
                DECToCP = DECValue / low_price_bcx;

            }



            public void ComputeDECValue()
            {

                var val = 0;

                switch (rarity)
                {
                    case 1:
                        val = 5;
                        break;
                    case 2:
                        val = 20;
                        break;
                    case 3:
                        val = 100;
                        break;
                    case 4:
                        val = 500;
                        break;
                    default:
                        throw new Exception("Could not find rarity");
                }

                DECValue = val * 25; // Remember, only gold cards are taken into account!


            }

        }

        public static double FindCardPrice(JToken JCard)
        {
            return double.Parse(JCard["buy_price"].ToString(), CultureInfo.InvariantCulture) / double.Parse(JCard["xp"].ToString());
        }

        public static int FindSpecificCardPower(JToken JCard, CardForRent Card)
        {
            return int.Parse(JCard["xp"].ToString()) * Card.DECValue;
        }

        //{"items":["ITEM1","ITEM2","ITEM3"],"currency":"CREDITS","days":1,"app":"splinterlands/0.7.139","n":"EoGADf5mf5"}

        public static async Task<bool> RentCards(List<string> cardsToRent, BotInstanceBlockchain bot)
        {
            var ids = "";
            foreach (string cardid in cardsToRent)
                ids += "\"" + cardid + "\"" + ",";

            ids = ids.Remove(ids.Length - 1);

            string n = Helper.GenerateRandomString(10);
            string json = "{\"items\":[" + ids + "],\"currency\":\"DEC\",\"days\":1,\"app\":\""+ Settings.SPLINTERLANDS_APP + "\",\"n\":\"" + n + "\"}";
            COperations.custom_json custom_Json = bot.CreateCustomJson(true, false, "sm_market_rent", json);
            string tx = Settings.oHived.broadcast_transaction(new object[] { custom_Json }, new string[] { bot.ActiveKey });

            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(5000);
                var transactionStatus = await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/transactions/lookup?trx_id=" + tx);
                if (transactionStatus.Contains(" not found"))
                    continue;

                if (!((string)transactionStatus).Contains("success\":true"))
                {
                    Log.WriteToLog("Error in renting! Player: " + bot.Username + " error: " + (string)transactionStatus);
                    return false;
                }
                else if (((string)transactionStatus).Contains("success\":true"))
                {
                    return true;
                }

            }
            return false;

        }


        public static async Task TestRent(BotInstanceBlockchain bot, int CPNeeded = 1000)
        {
            JToken allRentCards = JToken.Parse(await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/market/for_rent_grouped"));
            JToken allCardDetails = JToken.Parse(await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL + "/cards/get_details"));
            var filteredCardDetails = allCardDetails.Where(card =>
            {
                return card["editions"].ToString() == "7" || (card["editions"].ToString() == "3" && card["tier"].ToString() == "7");
            }).ToArray();

            Dictionary<int, JToken> IDToDetail = new Dictionary<int, JToken>();

            foreach (var card in filteredCardDetails)
                IDToDetail.Add(int.Parse(card["id"].ToString()), card);


            var cardsForRent = new List<CardForRent>(allRentCards.Where(card =>
            {
                var ID = int.Parse(card["card_detail_id"].ToString());
                var golden = card["gold"].ToString();
                var ChaosLegion = IDToDetail.ContainsKey(ID);

                var summoner = ChaosLegion ? IDToDetail[ID]["type"].ToString() == "Monster" : false;

                return golden == "True" && ChaosLegion && summoner;

            }).Select(x => new CardForRent(int.Parse(x["card_detail_id"].ToString()), //ID
                                           int.Parse(IDToDetail[int.Parse(x["card_detail_id"].ToString())]["rarity"].ToString()), //Find rarity in map
                                           double.Parse(x["low_price_bcx"].ToString()),
                                           IDToDetail[int.Parse(x["card_detail_id"].ToString())]["name"].ToString(), //Name
                                           x["edition"].ToString()))).ToArray();



            var sortedCards = cardsForRent.OrderByDescending(x => x.DECToCP).ToArray();
            (var cachedPower, _, _) = await SplinterlandsAPI.GetPlayerDetailsAsync(bot.Username);
            int upForRentPower = 0;

            var CardIDsToRent = new List<string>();

            while (cachedPower + upForRentPower < CPNeeded)
            {
                foreach (var card in sortedCards)
                {
                    if (card.DECValue + cachedPower + upForRentPower > CPNeeded * 1.1) // Dont want to overshoot on CP
                        continue;


                    JToken cardRentDetails = JToken.Parse(await Helper.DownloadPageAsync(Settings.SPLINTERLANDS_API_URL +
                        "/market/for_rent_by_card?card_detail_id=" + card.card_detail_id.ToString() + "&gold=true&edition=" + card.edition));

                    var rentalcards = new List<JToken>(cardRentDetails.OrderBy(card => FindCardPrice(card)).ToArray());

                    var bestCardPrice = FindCardPrice(rentalcards.First());
                    int upForRentCount = 0;
                    foreach (var validCard in rentalcards.Skip(5).ToArray()) // Skip the first 5, to avoid conflicts with other renters
                    {
                        if (cachedPower + upForRentPower >= CPNeeded || FindCardPrice(validCard) > bestCardPrice * 1.2 || upForRentCount > 40) // Enough power, or cards are now bad, or time to rent!
                            break;

                        if (FindSpecificCardPower(validCard, card) + cachedPower + upForRentPower > CPNeeded * 1.1) // This card is not needed, has too high power
                            continue;

                        CardIDsToRent.Add(validCard["market_id"].ToString());
                        upForRentPower += FindSpecificCardPower(validCard, card);
                        upForRentCount += 1;
                    }


                    if (cachedPower + upForRentPower >= CPNeeded)
                        break;

                }

                await RentCards(CardIDsToRent, bot);
                (cachedPower, _, _) = await SplinterlandsAPI.GetPlayerDetailsAsync(bot.Username);
                upForRentPower = 0;
            }



        }



        public static async Task testitAsync()
        {
            var CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync("raynie");
            var QuestCached = await SplinterlandsAPI.GetPlayerQuestAsync("soendergod");
            var splinters = new List<string> { "earth", "life", "death", "dragon", "water", "fire"};

            var manas = new List<int> { 15, 18, 24, 29, 33, 35, 39, 45, 99 }; 

            while (splinters.Count > 0)
            {

                Dictionary<string, int> monsterToPlayed = new Dictionary<string, int>();
                Dictionary<string, int> summonerToPlayed = new Dictionary<string, int>();
                Dictionary<string, int> colorPlayed = new Dictionary<string, int>();


                for (int i = 0; i < manas.Count(); i++)
                {
                    string username = GetRandomUsername();



                    var APIResponse = await BattleAPI.GetTeamFromAPIAsync(manas[i], "Standard", splinters.ToArray(), CardsCached, QuestCached.quest, QuestCached.questLessDetails, username);

                    string color = (string)APIResponse["color"];
                    string summonerID = (string)APIResponse["summoner_id"];
                    string monster1 = (string)APIResponse["monster_1_id"];
                    string monster2 = (string)APIResponse["monster_2_id"];
                    string monster3 = (string)APIResponse["monster_3_id"];
                    string monster4 = (string)APIResponse["monster_4_id"];
                    string monster5 = (string)APIResponse["monster_5_id"];
                    string monster6 = (string)APIResponse["monster_6_id"];

                    monsterToPlayed[monster1] = monsterToPlayed.ContainsKey(monster1) ? monsterToPlayed[monster1] + 1 : 1;
                    monsterToPlayed[monster2] = monsterToPlayed.ContainsKey(monster2) ? monsterToPlayed[monster2] + 1 : 1;
                    monsterToPlayed[monster3] = monsterToPlayed.ContainsKey(monster3) ? monsterToPlayed[monster3] + 1 : 1;
                    monsterToPlayed[monster4] = monsterToPlayed.ContainsKey(monster4) ? monsterToPlayed[monster4] + 1 : 1;
                    monsterToPlayed[monster5] = monsterToPlayed.ContainsKey(monster5) ? monsterToPlayed[monster5] + 1 : 1;
                    monsterToPlayed[monster6] = monsterToPlayed.ContainsKey(monster6) ? monsterToPlayed[monster6] + 1 : 1;

                    colorPlayed[color] = colorPlayed.ContainsKey(color) ? colorPlayed[color] + 1 : 1;

                    summonerToPlayed[summonerID] = summonerToPlayed.ContainsKey(summonerID) ? summonerToPlayed[summonerID] + 1 : 1;

                    Thread.Sleep(1000 * 60 * 2); //2 min sleep for API to reload.


                }

                var monsters = monsterToPlayed.ToList().OrderByDescending(kv => kv.Value).ToList();
                var summoners = summonerToPlayed.ToList().OrderByDescending(kv => kv.Value).ToList();
                var colors = colorPlayed.ToList().OrderByDescending(kv => kv.Value).ToList();

                var monsterstring = "";
                var summonerstring = "";
                var splinterstring = "";

                foreach(var monster in monsters)
                    monsterstring += monster.Key + " : " + monster.Value + "\n";

                foreach (var summ in summoners)
                    summonerstring += summ.Key + " : " + summ.Value + "\n";

                foreach (var sp in colors)
                    splinterstring += sp.Key + " : " + sp.Value + "\n";

                // Print to file:
                File.AppendAllText("Champions_data_mined.txt", "Splinters to chose from: " + splinters.Count.ToString() + "\n");
                File.AppendAllText("Champions_data_mined.txt", "Most chosen splinter: " + colors.First().Key + "\n");

                File.AppendAllText("Champions_data_mined.txt", "Monsters: " + monsterstring + "\n");
                File.AppendAllText("Champions_data_mined.txt", "Summoners: " + summonerstring + "\n");
                File.AppendAllText("Champions_data_mined.txt", "Splinters: " + splinterstring + "\n\n\n");


                splinters.Remove(colors.First().Key);

            }



        }
        public static string GetRandomUsername()
        {
            var randomUsernames = new List<string> { "xala949", "adai382", "soendergod", "lort321", "erdossplinter", "diracsplinter",
                                                      "dye2r", "xala940", "xala939", "xala938","xala937","xala935","xala933","xala921"
                                                            ,"xala922","xala923","xala924","xala925","xala926","xala927"};

            return randomUsernames[random.Next(0, randomUsernames.Count - 1)];
        }


        public static async Task TestItTwoAsync()
        {
            var splinters = new List<string> { "earth", "life", "death", "dragon", "water", "fire" };
            var manas = new List<int> { 14, 15, 18,19, 20, 21, 22, 23, 24,26, 28, 29, 33, 35, 39, 45, 99 };
            int j = 0;
            while (splinters.Count > 0)
            {

                for (int i = 0; i < manas.Count(); i++)
                {
                    var username = GetRandomUsername();
                    var CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(username);
                    var QuestCached = await SplinterlandsAPI.GetPlayerQuestAsync(username);
                    var qq = QuestCached.ToString();
                    var APIResponse = await BattleAPI.GetTeamFromAPIAsync(manas[i], "Standard", splinters.ToArray(), CardsCached, QuestCached.quest, QuestCached.questLessDetails, username);
                    j++;
                }
                splinters.Remove(splinters[0]);
            }
        }

    }


    
}
