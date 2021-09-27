﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    public static class API
    {
        private const string SplinterlandsAPI = "https://game-api.splinterlands.io";
        private const string SplinterlandsAPIFallback = "https://api2.splinterlands.com";

        public static async Task<JToken> GetTeamFromAPIAsync(int mana, string rules, string[] splinters, string[] cards, JToken quest, string username)
        {
            Log.WriteToLog($"{username}: Requesting team from API...");
            try
            {   
                var matchDetails = new JObject(
                    new JProperty("mana", mana),
                    new JProperty("rules", rules),
                    new JProperty("splinters", splinters),
                    new JProperty("myCards", cards),
                    new JProperty("quest", Settings.PrioritizeQuest && quest != null
                    && ((int)quest["total"] != (int)quest["completed"]) ? 
                   quest : "")
                );

                string APIResponse = await PostJSONToApi(matchDetails, $"{Settings.APIUrl}get_team/",  username);
                if (APIResponse == null)
                {
                    Log.WriteToLog($"{username}: API Error: Response was empty", Log.LogType.CriticalError);
                    return null;
                }

                Log.WriteToLog($"{username}: API Response: {APIResponse}");

                return JToken.Parse(APIResponse);
            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: API Error: {ex}", Log.LogType.CriticalError);
            }
            return null;
        }

        public static async Task<JToken> GetPlayerQuestAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/players/quests?username={ username }");
                if (data == null || data.Trim().Length < 10)
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for quest, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/players/quests?username={ username }");
                }
                JToken quest = JToken.Parse(data);
                var questLessDetails = new JObject(
                    new JProperty("name", quest[0]["name"]),
                    new JProperty("splinter", Settings.QuestTypes[(string)quest[0]["name"]]),
                    new JProperty("total", quest[0]["total_items"]),
                    new JProperty("completed", quest[0]["completed_items"])
                    );
                return questLessDetails;

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get quest from splinterlands api: {ex}", Log.LogType.Error);
            }
            return null;
        }

        public static async Task<string[]> GetPlayerCardsAsync(string username)
        {
            try
            {
                string data = await DownloadPageAsync($"{SplinterlandsAPI}/cards/collection/{ username }");
                if (data == null || data.Trim().Length < 10)
                {
                    // Fallback API
                    Log.WriteToLog($"{username}: Error with splinterlands API for cards, trying fallback api...", Log.LogType.Warning);
                    data = await DownloadPageAsync($"{SplinterlandsAPIFallback}/cards/collection/{ username }");
                }

                DateTime oneDayAgo = DateTime.Now.AddDays(-1);
                string[] cards = JToken.Parse(data)["cards"].Where(x =>
                (x["delegated_to"].Type == JTokenType.Null || (string)x["delegated_to"] == username) &&
                x["market_listing_type"].Type == JTokenType.Null && 
                    !((string)x["last_used_player"] != username && 
                        (
                            x["last_used_date"].Type != JTokenType.Null && 
                            DateTime.Parse(JsonConvert.SerializeObject(x["last_used_date"]).Replace("\"", "").Trim()) > oneDayAgo
                        )
                    )
                )
                .Select(x => (string)x["card_detail_id"]).Distinct().ToArray();
                var combinedCards = new string[cards.Length + Settings.PhantomCards.Length];
                cards.CopyTo(combinedCards, 0);
                Settings.PhantomCards.CopyTo(combinedCards, cards.Length);
                return combinedCards;

            }
            catch (Exception ex)
            {
                Log.WriteToLog($"{username}: Could not get cards from splinterlands api: {ex}{Environment.NewLine}Bot will play with phantom cards only.", Log.LogType.Error);
            }
            return Settings.PhantomCards;
        }

        private readonly static HttpClient _httpClient = new HttpClient();
        private async static Task<string> DownloadPageAsync(string url)
        {
            // Use static HttpClient to avoid exhausting system resources for network connections.
            var result = await _httpClient.GetAsync(url);
            var response = await result.Content.ReadAsStringAsync();
            // Write status code.
            return response;
        }

        private async static Task<string> PostJSONToApi(object json, string url, string username)
        {
            using (var content = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage result = await _httpClient.PostAsync(url, content);
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string returnValue = await result.Content.ReadAsStringAsync();
                    return returnValue;
                }
                Log.WriteToLog($"{username}: Failed to POST data to API: ({result.StatusCode})");
            }
            return null;
        }
    }
}