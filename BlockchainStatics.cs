using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes;

namespace Ultimate_Splinterlands_Bot_V2
{
 
    public static class BlockchainSaticMethods
    {

        public static async Task<int> FindAccWithMaxECRAsync()
        {
            double maxECR = -1;
            int maxBot = -1;

            foreach(var bot in Settings.BotInstancesBlockchain)
            {
                double botECR = await bot.GetECRFromAPIAsync();

                if (botECR > maxECR && botECR >= Settings.ECRThreshold + 5) // + 5, dont wanna just trade cards for no reason.
                {
                    maxECR = botECR;
                    maxBot = Settings.BotInstancesBlockchain.IndexOf(bot);
                }
            }
            return maxBot;
        }

        public static async Task<List<int>> FindAccsUnderThresholdAsync(int ratingThreshold = 1000)
        {
            var lowRatedIndexes = new List<int>();


            foreach (var bot in Settings.BotInstancesBlockchain)
            {
                double botRating = await bot.GetECRFromAPIAsync();

                if (botRating < ratingThreshold)
                {
                    lowRatedIndexes.Append(Settings.BotInstancesBlockchain.IndexOf(bot));
                }
            }
            return lowRatedIndexes;
        }

        public static async Task<bool> TransferCards(string toUser, BotInstanceBlockchain fromUser, List<string> cardUUIDs)
        {
            try
            {
                await fromUser.TransferCards(toUser, cardUUIDs);
                Log.WriteToLog("Cards transfered from: " + fromUser.Username + " to: " + toUser + " cards: " + String.Join(", ", cardUUIDs));
                return true;

            }
            catch (Exception ex)
            {
                Log.WriteToLog("Cards transfer error. From: " + fromUser.Username + " to: " + toUser + " cards: " + String.Join(", ", cardUUIDs) + " error: " + ex.ToString());
                return false;
            }
        }

        public static async Task<bool> TransferPowerSticksAsync(string toUser, BotInstanceBlockchain fromUser)
        {
            Log.WriteToLog("Trading power sticks from: " + fromUser.Username + " to: " + toUser + Environment.NewLine);

            var mustBeTransfered = await FindPowerSticksToTransferAsync(fromUser.Username);


            if (mustBeTransfered.Count == 0)
            {
                Log.WriteToLog("No cards could be traded." + Environment.NewLine);
                return true;
            }


            var result = await TransferCards(toUser, fromUser, mustBeTransfered.Select(c => c.card_long_id).ToList());
            Log.WriteToLog("All power sticks traded to next bot." + Environment.NewLine);
            return result;
        }
        public static async Task<bool> TransferAllPowerSticksMasterAsync(int masterID = 0)
        {
            Log.WriteToLog("Trading all power sticks from accounts to master..." + Environment.NewLine);

            var bots = Settings.BotInstancesBlockchain;
            var masterBotInfo = bots[masterID];

            for (int i = 0; i < bots.Count; i++)
            {
                if (i == masterID)
                    continue;

                var result = await TransferPowerSticksAsync(masterBotInfo.Username, bots[i]);

                if (!result)
                    return false;
            }
            Log.WriteToLog("All cards traded to master." + Environment.NewLine);
            return true;


        }



        private static async Task<List<Card>> FindPowerSticksToTransferAsync(string username)
        {
            // Gets all cards of an account
            var CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(username, false, false);

            var mustBeTransfered = new List<Card>();


            // Check if the account has any card which should be transfered.
            foreach (var c in Settings.PowerStickCards)
            {
                if (CardsCached.Where(cachedcard => cachedcard.card_long_id == c.card_long_id).Count() > 0)
                    mustBeTransfered.Add(c);
            }
            return mustBeTransfered;
        }

        public static async Task TransferRewardsToGlobalMaster(int localMasterID = 0)
        {
            Log.WriteToLog("Transfering all rewards from local master to global master..." + Environment.NewLine);

            var globalMaster = Settings.MasterAccount;
            var localMaster = Settings.BotInstancesBlockchain[localMasterID];

            if (globalMaster.Username == localMaster.Username)
            {
                Log.WriteToLog("Global master is equal to local master on this system!" + Environment.NewLine);
                return;
            }

            var mustBeTransfered = await SplinterlandsAPI.GetPlayerCardsAsync(localMaster.Username, false, true, false, true);
            if (mustBeTransfered.Count() == 0) return;
            var result = await TransferCards(globalMaster.Username, localMaster, mustBeTransfered.Select(c => c.card_long_id).ToList());

            var DEC = await localMaster.GetDECFromAPIAsync();
            if (DEC == -1)
            {
                Log.WriteToLog("Could not get DEC from: " + localMaster.Username);
            }
            await localMaster.SendDEC(globalMaster.Username, DEC);
            Log.WriteToLog("DEC transfered from: " + localMaster.Username + " to: " + globalMaster.Username + " amn: " + (DEC - 0.1).ToString());
        }

        public static async Task<bool> TransferAllCardsToMaster(int masterID = 0)
        {

            Log.WriteToLog("Transfering all cards from accounts to master..." + Environment.NewLine);

            var bots = Settings.BotInstancesBlockchain;
            var masterBotInfo = bots[masterID];

            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i].Username == masterBotInfo.Username)
                    continue;

                var botinfo = bots[i];

                var mustBeTransfered = await SplinterlandsAPI.GetPlayerCardsAsync(botinfo.Username, false, false, false, true);

                // No cards should be transfered.
                if (mustBeTransfered.Count() == 0) continue;

                var result = await TransferCards(masterBotInfo.Username, botinfo, mustBeTransfered.Select(c => c.card_long_id).ToList());

                if (!result)
                    return false;

            }
            Log.WriteToLog("All cards traded to master." + Environment.NewLine);
            return true;
            
        }

        public static async Task<bool> TransferRewardCardsToMaster(int masterID = 0)
        {

            Log.WriteToLog("Transfering all non power stick cards from accounts to master..." + Environment.NewLine);

            var bots = Settings.BotInstancesBlockchain;
            var masterBotInfo = bots[masterID]; 

            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i].Username == masterBotInfo.Username)
                    continue;

                var botinfo = bots[i];

                var mustBeTransfered = await SplinterlandsAPI.GetPlayerCardsAsync(botinfo.Username, false, true, false, true);

                // No cards should be transfered.
                if (mustBeTransfered.Count() == 0) continue;

                var result = await TransferCards(masterBotInfo.Username, botinfo, mustBeTransfered.Select(c => c.card_long_id).ToList());

                if (!result)
                    return false;

            }
            Log.WriteToLog("All cards traded to master." + Environment.NewLine);
            return true;

        }

        public static async Task ClaimSeasonRewardsAll()
        {
            var bots = Settings.BotInstancesBlockchain;
            
            foreach(var bot in bots)
            {
                await bot.ClaimSeasonReward();
            }
        }

        public static async Task SendAllDEC(int masterID = 0)
        {
            var bots = Settings.BotInstancesBlockchain;
            var masterBot = Settings.BotInstancesBlockchain[masterID];
            double total = 0;

            foreach (var bot in bots)
            {
                if (bot.Username == masterBot.Username)
                    continue;

                var DEC = await bot.GetDECFromAPIAsync();
                if (DEC == -1)
                {
                    Log.WriteToLog("Could not get DEC from: " + bot.Username);
                    continue;
                }

                try
                {
                    if ((DEC - 0.1) <= 0)
                        continue;
                    await bot.SendDEC(masterBot.Username, DEC);
                    Log.WriteToLog("DEC transfered from: " + bot.Username + " to: " + masterBot.Username + " amn: " + (DEC - 0.1).ToString());
                    total += DEC;
                }
                catch (Exception ex)
                {
                    Log.WriteToLog("Error in trading DEC from: " + bot.Username + " to: " + masterBot.Username + " amn: " + DEC.ToString() + " err: " + ex.ToString());
                }


            }
            Log.WriteToLog("Total send: " + total.ToString());
        }

        public static async Task GetOverview()
        {
            Log.WriteToLog("Getting overview of bots.");
            double avgRating = 0;
            double avgECR = 0;
            foreach (var bot in Settings.BotInstancesBlockchain)
            {
                (_, var RatingCached, _) = await SplinterlandsAPI.GetPlayerDetailsAsync(bot.Username);
                var ECR = await bot.GetECRFromAPIAsync();
                avgRating += RatingCached;
                avgECR += ECR;
                Log.WriteToLog("User: " + bot.Username);
                Log.WriteToLog("ECR: " + ECR);
                Log.WriteToLog("Rating: " + RatingCached + Environment.NewLine);

            }
            Log.WriteToLog("Avg rating of all bots: " + (avgRating/Settings.BotInstancesBlockchain.Count).ToString());
            Log.WriteToLog("Avg ECR of all bots: " + (avgECR / Settings.BotInstancesBlockchain.Count).ToString());

        }

        public static async Task AdvanceAllLeauges()
        {
            Log.WriteToLog("Advancing Leagues.");
            var bots = Settings.BotInstancesBlockchain;

            foreach (var bot in bots)
            {
                (var powerCached, var RatingCached, var LeagueCached) = await SplinterlandsAPI.GetPlayerDetailsAsync(bot.Username);
                bot.RatingCached = RatingCached;
                bot.LeagueCached = LeagueCached;
                bot.PowerCached = powerCached;
                await bot.AdvanceLeague();

            }

        }

        public static async Task DelegateDECToAll(double DECAmount, int masterID = 0)
        {
            Log.WriteToLog("Delegating DEC from master, amount: " + DECAmount.ToString());
            var bots = Settings.BotInstancesBlockchain;
            var masterBot = bots[masterID];



            foreach (var bot in bots)
            {
                if (bot.Username == masterBot.Username)
                    continue;

                try
                {
                    await masterBot.SendDEC(bot.Username, DECAmount);
                    Log.WriteToLog("DEC transfered from: " + masterBot.Username + " to: " + bot.Username + " amn: " + DECAmount.ToString());
                }
                catch (Exception ex)
                {
                    Log.WriteToLog("Error in trading DEC from: " + masterBot.Username + " to: " + bot.Username + " amn: " + DECAmount.ToString() + " err: " + ex.ToString());
                }

            }

        }



    }
}
