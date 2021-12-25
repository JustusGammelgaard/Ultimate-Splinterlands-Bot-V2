using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
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
            var randomUsernames = new List<string> { "nnats", "crom228", "crom229", "crom230", "camap802","phuc588","phuc430","phuc456", "vindicator",
                                                    "breadtime", "ggez", "kama533", "potatokiller", "cleaninglady", "frodo", "zonsan62", "zonsan70",
                                                    "crom145", "crom315", "crom221", "crom548", "crom781", "omun021", "omun312", "lazking501", "lazking502", "lazking351",
                                                    "qhunter011", "qhunter015"};

            return randomUsernames[random.Next(0, randomUsernames.Count - 1)];
        }



    }
}
