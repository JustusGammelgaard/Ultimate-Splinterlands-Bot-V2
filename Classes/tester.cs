using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
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
            var CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync("soendergod");
            var QuestCached = await SplinterlandsAPI.GetPlayerQuestAsync("soendergod");
            var splinters = new string[4];
            splinters[0] = "earth";
            splinters[1] = "life";
            splinters[2] = "death";
            splinters[3] = "dragon";

            var manas = Enumerable.Range(13, 40).ToList();

            for (int i = 0; i < manas.Count(); i++)
            {
                string username = "soendergod";



                var APIResponse = await BattleAPI.GetTeamFromAPIAsync(manas[i], "Noxious Fumes", splinters, CardsCached, QuestCached.quest, QuestCached.questLessDetails, username);
                

            }
        }
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }



    }
}
