using Newtonsoft.Json.Linq;
using Pastel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ultimate_Splinterlands_Bot_V2.Classes
{
    class Log
    {
        private static object _ConsoleLock = new object();
        public enum LogType
        {
            Success,
            Information,
            Error,
            CriticalError,
            Warning
        };

        /// <summary>
        /// Writes text to the log
        /// </summary>
        /// <param name="Message">The text to write to the log</param>
        /// <param name="logType">1 = Success / Green, 2 = Information / Default Color, 3 = Error / Red, 4 = Critical Error / DarkViolet, 5 = Warning / Orange, default = Default Color</param>
        public static void WriteToLog(string message, LogType logType = LogType.Information, bool debugOnly = false, string logFile = @"/log.txt")
        {
            if (debugOnly && !Settings.DebugMode)
            {
                return;
            }

            string messagePrefix = $"[{ DateTime.Now }] ";

            Color textColor;

            switch (logType)
            {
                case LogType.Success:
                    textColor = Color.Green;
                    break;
                case LogType.Information:
                    textColor = Color.LightGray;
                    break;
                case LogType.Error:
                    textColor = Color.Red;
                    messagePrefix += "Error: ".Pastel(textColor);
                    break;
                case LogType.CriticalError:
                    textColor = Color.Magenta;
                    messagePrefix += "Critical Error: ".Pastel(textColor);
                    break;
                case LogType.Warning:
                    textColor = Color.Yellow;
                    messagePrefix += "Warning: ".Pastel(textColor);
                    break;
                default:
                    textColor = Color.LightGray;
                    break;
            }

            lock (_ConsoleLock)
            {
                Console.WriteLine(messagePrefix + message.Pastel(textColor));

                if (Settings.WriteLogToFile)
                {
                    System.IO.File.AppendAllText(Settings.StartupPath + logFile, messagePrefix + message + Environment.NewLine);
                }
            }
        }

        public static void LogBattleSummaryToTable()
        {
            try
            {
                var t = new TablePrinter("#", "Account", "Result", "Rating", "ECR", "QuestStatus");
                Settings.LogSummaryList.ForEach(x => t.AddRow(x.index, x.account, x.battleResult, x.rating, x.ECR, x.questStatus));
                Settings.LogSummaryList.Clear();
                lock (_ConsoleLock)
                {
                    t.Print();
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog("Error at Battle Summary: " + ex.ToString(), LogType.Error);
            }
        }

        public static void LogChampions(JToken team)
        {
            for (int i = 1; i < 7; i++)
            {
                var monsterName = (string)team[$"monster_{i}_id"];
                if (monsterName == "")
                {
                    break;
                }
                //Print til csv
                System.IO.File.AppendAllText(Settings.StartupPath + @"/championLog.txt", monsterName + ",");
            }

            System.IO.File.AppendAllText(Settings.StartupPath + @"/summonerLog.txt", (string)team[$"summoner_id"] + ",");
            System.IO.File.AppendAllText(Settings.StartupPath + @"/splinterLog.txt", (string)team[$"color"] + ",");


        }

        public static void LogTeamToTable(JToken team, int mana, string rulesets)
        {
            var t = new TablePrinter("Mana", "Rulesets", "Quest Prio", "Win %", "Team Rank");
            t.AddRow(mana, rulesets, team["play_for_quest"], (Convert.ToDouble(((string)team["summoner_wins"]).Replace(",", "."), CultureInfo.InvariantCulture) * 100).ToString("N2"), team["teamRank"]);
            lock (_ConsoleLock)
            {
                t.Print();
            }
            t = new TablePrinter("Card", "ID", "Name", "Element");
            t.AddRow("Summoner", (string)team["summoner_id"], (string)Settings.CardsDetails[((int)team["summoner_id"]) - 1]["name"],
            ((string)Settings.CardsDetails[((int)team["summoner_id"]) - 1]["color"])
            .Replace("Red", "Fire").Replace("Blue", "Water").Replace("White", "Life").Replace("Black", "Death").Replace("Green", "Earth").Replace("Gold", "Dragon"));
            for (int i = 1; i < 7; i++)
            {
                if ((string)team[$"monster_{i}_id"] == "")
                {
                    break;
                }
                t.AddRow($"Monster #{i}", (string)team[$"monster_{i}_id"], (string)Settings.CardsDetails[((int)team[$"monster_{i}_id"]) - 1]["name"],
                ((string)Settings.CardsDetails[((int)team[$"monster_{i}_id"]) - 1]["color"])
                .Replace("Red", "Fire").Replace("Blue", "Water").Replace("White", "Life").Replace("Black", "Death").Replace("Green", "Earth")
                .Replace("Gray", "Neutral").Replace("Gold", "Dragon"));
            }

            lock (_ConsoleLock)
            {
                t.Print();
            }
        }





        public static void Help()
        {
            WriteToLog("----------------------------HELP-----------------------------");
            WriteToLog("------------------------ABBREVATIONS-------------------------");
            WriteToLog("Local master  : The first bot in accounts.txt");
            WriteToLog("Global master : The bot defined in master.txt");
            WriteToLog("--------------------------COMMANDS---------------------------");
            WriteToLog("-battle       : Start the bot for battle.");
            WriteToLog("-battle_EOS   : Start the bot for battle in EOS mode. All bots will play until 40ECR has been reached, or the bot has obtained +1K rating. [NOT YET TESTED]");
            WriteToLog("-claim        : Claim season rewards.");
            WriteToLog("-t_global     : Transfer all rewards (cards + DEC) to global master, from local master. [NOT YET TESTED]");
            WriteToLog("-t_all_cards  : Transfer ALL cards to local master.");
            WriteToLog("-t_rew_cards  : Transfer ALL non-power sticks to local master");
            WriteToLog("-t_DEC        : Transfer ALL DEC to local master.");
            WriteToLog("-overview     : Get overview - Rating, ECR, etc, for all accounts.");
            WriteToLog("-advance      : Advance all accounts to their max leauge (Must be used after renting at EoS).");
            WriteToLog("-d_DEC        : Delegate DEC to each account (in accounts.txt, amn is send from local master). The specified amount should be written in config.txt under DEC_DELEGATE_AMOUNT=. (Should be used before renting)");
            WriteToLog("----------------------------INFO-----------------------------");
            WriteToLog("Arguments can be passed in succesion:");
            WriteToLog("-claim -t_rew_cards -battle : will first claim rewards, send reward cards to local master, then battle.");
            WriteToLog("---------------------End of Season (EoS)---------------------");
            WriteToLog("1-2 days before EoS, -battle_EOS should be used. Ensures that all accounts can get good rewards");
            WriteToLog("<1 days before EoS, -d_DEC, -rent_EoS [not yet implemented], -advance must ALL be used, in this order! (Otherwise, no rewards). Each system must use this");
            WriteToLog("After EoS, -claim should be used.");
            WriteToLog("Then, -battle, to start the bot for the next season.");
            WriteToLog("Rewards can be sent to global by: -t_rew_cards, -t_DEC, -t_global");


        }
    }
}
