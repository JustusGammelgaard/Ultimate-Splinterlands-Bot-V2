using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Ultimate_Splinterlands_Bot_V2.Classes;
using System.Threading;
using Pastel;
using System.Drawing;
using System.Reflection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;

namespace Ultimate_Splinterlands_Bot_V2
{
    class Program
    {
        private static object _TaskLock = new object();
        static async Task Main(string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handler = new ConsoleEventDelegate(ConsoleEventCallback);
                SetConsoleCtrlHandler(handler, true);
                if (Environment.OSVersion.Version.Major < 10)
                {
                    Console.WriteLine("Legacy mode for old Windows version activated - please update your Windows to Windows 10 or higher / Windows Server 2016 or higher to get maximum bot speed");
                    Settings.LegacyWindowsMode = true;
                    ConsoleExtensions.Disable();
                }
            }

            SetStartupPath();

            // We have to configure the http client early because it might be used in account constructor
            Settings._httpClient.Timeout = new TimeSpan(0, 2, 15);

            if (!ReadConfig() || (Settings.BrowserMode && !CheckForChromeDriver()) || !ReadAccounts() || !ReadTransferConfig() || !ReadMasterAccount())
            {
                Log.WriteToLog("Press any key to close");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (Settings.LightningMode && Settings.ClaimSeasonReward)
            {
                Log.WriteToLog("Season Reward Claiming mode activated - set CLAIM_SEASON_REWARD=false to disable!", Log.LogType.Warning);
                Log.WriteToLog("The bot will only claim rewards, it will not fight!", Log.LogType.Warning);
                Thread.Sleep(3500);
            }

            Thread.Sleep(1500); // Sleep 1.5 seconds to read config and welcome message

            Initialize();
            await tester.TestRent(Settings.BotInstancesBlockchain[1]);

            // await tester.TestItTwoAsync();

            if (args.Count() == 0)
                Log.Help();

            foreach(string arg in args)
            {
                switch (arg)
                {
                    case "-battle":
                        await BotLoopTransfer();
                        break;
                    case "-battle_EOS":
                        await BotLoopEOS();
                        break;
                    case "-t_global":
                        await BlockchainSaticMethods.TransferRewardsToGlobalMaster();
                        break;
                    case "-t_all_cards":
                        await BlockchainSaticMethods.TransferAllCardsToMaster();
                        break;
                    case "-t_rew_cards":
                        await BlockchainSaticMethods.TransferRewardCardsToMaster();
                        break;
                    case "-claim":
                        await BlockchainSaticMethods.ClaimSeasonRewardsAll();
                        break;
                    case "-t_DEC":
                        await BlockchainSaticMethods.SendAllDEC();
                        break;
                    case "-overview":
                        await BlockchainSaticMethods.GetOverview();
                        break;
                    case "-advance":
                        await BlockchainSaticMethods.AdvanceAllLeauges();
                        break;
                    case "-d_DEC":
                        await BlockchainSaticMethods.DelegateDECToAll(Settings.DECDelegateAmount);
                        break;
                    default:
                        Log.Help();
                        break;
                }

            }



            // await TransferCardsBlockchain.TransferAllCardsToMaster();


        }

        static async Task BotLoopTransfer()
        {
            int battleCrashes = 0;

            while (true) 
            {
                bool transferSuccess = true;
                var timer = DateTime.Now;
                int currentBot = 0;
                Settings.BotInstancesBlockchain[0].hasCards = true;

                // On start up and every 12 hours, a check is performed.
                // Master receives all cards, and the process starts with the master.
                // If a transfer fails, this is called (so all cards can be returned)

                bool masterTransfered = false;
                int masterTransferFailures = 0;
                while (!masterTransfered)
                {
                    masterTransfered = await BlockchainSaticMethods.TransferAllPowerSticksMasterAsync();

                    if (masterTransfered) break;

                    Thread.Sleep(1000 * 60 * 10); //10 min sleep
                    masterTransferFailures += 1;

                    if (masterTransferFailures >= 10)
                    {
                        throw new Exception("Errors in master transfering.. Aborting.");
                    }
                }

                while ((DateTime.Now - timer).TotalHours < 12 && transferSuccess)
                {

                    while (SplinterlandsAPI.CheckForMaintenance().Result)
                    {
                        Log.WriteToLog("Splinterlands maintenance - waiting 3 minutes");
                        Thread.Sleep(3 * 60000);
                    }

                    // Battle!
                    try
                    {
                        var result = await Settings.BotInstancesBlockchain[currentBot].DoBattleAsync(0, false, currentBot);
                        Log.LogBattleSummaryToTable();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteToLog("Error in battle: " + ex.ToString());
                        Log.WriteToLog("Sleeping for 10 min");
                        Thread.Sleep(1000 * 60 * 10);
                        battleCrashes++;

                        if (battleCrashes > 5)
                            Environment.Exit(100);
                        continue;
                    }



                    double ECRCached = await Settings.BotInstancesBlockchain[currentBot].GetECRFromAPIAsync();
                    if (ECRCached < Settings.ECRThreshold)
                    {
                        // Transfer cards to acc with most ECR. If all have below threshold, sleep for a long time. 
                        var newBot = await BlockchainSaticMethods.FindAccWithMaxECRAsync();
                        // No bots have more than threshold
                        if (newBot == -1)
                        {
                            Log.WriteToLog("No account with enough ECR found. Sleeping for 20 min.");
                            Thread.Sleep(1000 * 60 * 20); // sleep 20 min
                        }
                        else if (newBot == currentBot) ; // pass - something weird happened.
                        else
                        {
                            transferSuccess = await BlockchainSaticMethods.TransferPowerSticksAsync(Settings.BotInstancesBlockchain[newBot].Username, Settings.BotInstancesBlockchain[currentBot]);

                            currentBot = newBot;

                        }
                        continue;
                    }

                   
                    while (Settings.BotInstancesBlockchain[currentBot].SleepUntil > DateTime.Now)
                        Thread.Sleep(1000 * Settings._Random.Next(5, 10)); // 5-10s sleep. 
                }
            }

        }

        static async Task BotLoopEOS()
        {
            int battleCrashes = 0;

            while (true)
            {
                bool transferSuccess = true;
                var timer = DateTime.Now;
                int currentBot = 0;
                Settings.BotInstancesBlockchain[0].hasCards = true;


                bool masterTransfered = false;
                int masterTransferFailures = 0;
                while (!masterTransfered)
                {
                    masterTransfered = await BlockchainSaticMethods.TransferAllPowerSticksMasterAsync();

                    if (masterTransfered) break;

                    Thread.Sleep(1000 * 60 * 10); //10 min sleep
                    masterTransferFailures += 1;

                    if (masterTransferFailures >= 10)
                    {
                        throw new Exception("Errors in master transfering.. Aborting.");
                    }
                }

                while ((DateTime.Now - timer).TotalHours < 12 && transferSuccess)
                {

                    while (SplinterlandsAPI.CheckForMaintenance().Result)
                    {
                        Log.WriteToLog("Splinterlands maintenance - waiting 3 minutes");
                        Thread.Sleep(3 * 60000);
                    }

                    // Battle!
                    try
                    {
                        var result = await Settings.BotInstancesBlockchain[currentBot].DoBattleAsync(0, false, currentBot);
                        Log.LogBattleSummaryToTable();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteToLog("Error in battle: " + ex.ToString());
                        Log.WriteToLog("Sleeping for 10 min");
                        Thread.Sleep(1000 * 60 * 10);
                        battleCrashes++;

                        if (battleCrashes > 5)
                            Environment.Exit(100);
                        continue;
                    }



                    double ECRCached = await Settings.BotInstancesBlockchain[currentBot].GetECRFromAPIAsync();
                    (_, var RatingCached, _) = await SplinterlandsAPI.GetPlayerDetailsAsync(Settings.BotInstancesBlockchain[currentBot].Username);

                    if (ECRCached < 40 || RatingCached > 1000)
                    {
                        var newBots = await BlockchainSaticMethods.FindAccsUnderThresholdAsync();

                        if (newBots.Count == 0)
                        {
                            Log.WriteToLog("All accounts have 1K+ Rating. Stopping...");
                            return;
                        }

                        // Find bot with max ECR
                        double maxECR = -1;
                        var newBot = -1;
                        foreach(int b in newBots)
                        {
                            double ECR = await Settings.BotInstancesBlockchain[b].GetECRFromAPIAsync();
                            
                            if (ECR > maxECR)
                            {
                                maxECR = ECR;
                                newBot = b;
                            }
                        }

                        if (newBot == -1)
                        {
                            Log.WriteToLog("Could not find newBot...");
                            return;
                        }

                        if(maxECR < 40)
                        {
                            Thread.Sleep(1000 * 60 * 20); //Bots need to play, but None has enough ECR.  
                        }
                        else if (newBot == currentBot) ; // pass - something weird happened.
                        else
                        {
                            transferSuccess = await BlockchainSaticMethods.TransferPowerSticksAsync(Settings.BotInstancesBlockchain[newBot].Username, Settings.BotInstancesBlockchain[currentBot]);
                            currentBot = newBot;

                        }
                        continue;
                    }


                    while (Settings.BotInstancesBlockchain[currentBot].SleepUntil > DateTime.Now)
                        Thread.Sleep(1000 * Settings._Random.Next(5, 10)); // 5-10s sleep. 
                }
            }

        }

        static bool ReadConfig()
        {
            string filePath = Settings.StartupPath + @"/config/config.txt";
            if (!File.Exists(filePath))
            {
                Log.WriteToLog("No config.txt in config folder - see config-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            Log.WriteToLog("Reading config...");
            foreach (string setting in File.ReadAllLines(filePath))
            {
                string[] temp = setting.Split('=');
                if (temp.Length != 2 || setting[0] == '#')
                {
                    continue;
                }

                switch (temp[0])
                {
                    case "PRIORITIZE_QUEST":
                        Settings.PrioritizeQuest = Boolean.Parse(temp[1]);
                        break;
                    case "SLEEP_BETWEEN_BATTLES":
                        Settings.SleepBetweenBattles = Convert.ToInt32(temp[1]);
                        break;
                    case "ECR_THRESHOLD":
                        Settings.ECRThreshold = Convert.ToInt32(temp[1]);
                        break;
                    case "DEC_DELEGATE_AMOUNT":
                        Settings.DECDelegateAmount = Convert.ToInt32(temp[1]);
                        break;
                    // legacy:
                    case "ERC_THRESHOLD":
                        Settings.ECRThreshold = Convert.ToInt32(temp[1]);
                        break;
                    case "MAX_BROWSER_INSTANCES":
                        Settings.MaxBrowserInstances = Convert.ToInt32(temp[1]);
                        break;
                    case "CLAIM_SEASON_REWARD":
                        Settings.ClaimSeasonReward = Boolean.Parse(temp[1]);
                        break;
                    case "CLAIM_QUEST_REWARD":
                        Settings.ClaimQuestReward = Boolean.Parse(temp[1]);
                        break;
                    case "DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE":
                        Settings.DontClaimQuestNearHigherLeague = Boolean.Parse(temp[1]);
                        break;
                    case "WAIT_FOR_MISSING_CP_AT_QUEST_CLAIM":
                        Settings.WaitForMissingCPAtQuestClaim = Boolean.Parse(temp[1]);
                        break;
                    case "ADVANCE_LEAGUE":
                        Settings.AdvanceLeague = Boolean.Parse(temp[1]);
                        break;
                    case "REQUEST_NEW_QUEST":
                        Settings.BadQuests = temp[1].Split(',');
                        break;
                    case "USE_LIGHTNING_MODE":
                        Settings.LightningMode = Boolean.Parse(temp[1]);
                        break;
                    case "SHOW_BATTLE_RESULTS":
                        Settings.ShowBattleResults = Boolean.Parse(temp[1]);
                        break;
                    case "THREADS":
                        Settings.Threads = Convert.ToInt32(temp[1]);
                        break;
                    case "USE_BROWSER_MODE":
                        Settings.BrowserMode = Boolean.Parse(temp[1]);
                        break;
                    case "HEADLESS":
                        Settings.Headless = Boolean.Parse(temp[1]);
                        break;
                    case "USE_API":
                        Settings.UseAPI = Boolean.Parse(temp[1]);
                        break;
                    case "API_URL":
                        Settings.PublicAPIUrl = temp[1];
                        break;
                    case "DEBUG":
                        Settings.DebugMode = Boolean.Parse(temp[1]);
                        break;
                    case "WRITE_LOG_TO_FILE":
                        Settings.WriteLogToFile = Boolean.Parse(temp[1]);
                        break;
                    case "DISABLE_CONSOLE_COLORS":
                        if (Boolean.Parse(temp[1]))
                        {
                            Log.WriteToLog("Console colors disabled!");
                            ConsoleExtensions.Disable();
                        }
                        break;
                    case "SHOW_API_RESPONSE":
                        Settings.ShowAPIResponse = Boolean.Parse(temp[1]);
                        break;
                    case "CHROME_BINARY_PATH":
                        Settings.ChromeBinaryPath = temp[1];
                        break;
                    case "CHROME_DRIVER_PATH":
                        Settings.ChromeDriverPath = temp[1];
                        break;
                    case "CHROME_NO_SANDBOX":
                        Settings.ChromeNoSandbox = Boolean.Parse(temp[1]);
                        break;
                    case "RENTAL_BOT_DLL_PATH":
                        Settings.RentalBotDllPath = temp[1];
                        break;
                    case "RENTAL_BOT":
                        if (Boolean.Parse(temp[1]))
                        {
                            SetupRentalBot();
                        }
                        break;
                    case "USE_PRIVATE_API":
                        Settings.UsePrivateAPI = Boolean.Parse(temp[1]);
                        if (Settings.UsePrivateAPI)
                        {
                            string[] loginData = File.ReadAllText(Settings.StartupPath + @"/config/login.txt").Split(':');
                            Settings.PrivateAPIUsername = loginData[0];
                            Settings.PrivateAPIPassword = loginData[1];
                        }
                        break;
                    case "PRIVATE_API_SHOP":
                        Settings.PrivateAPIShop = temp[1];
                        break;
                    case "PRIVATE_API_URL":
                        Settings.PrivateAPIUrl = temp[1];
                        break;
                    case "RENT_DAYS":
                        Settings.DaysToRent = Convert.ToInt32(temp[1]);
                        break;
                    case "RENT_POWER":
                        Settings.DesiredRentalPower = Convert.ToInt32(temp[1]);
                        break;
                    case "RENT_MAX_PRICE_PER_500":
                        Settings.MaxRentalPricePer500 = Convert.ToDecimal(temp[1], System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    default:
                        break;
                }
            }

            if (Settings.BrowserMode == Settings.LightningMode)
            {
                Log.WriteToLog("Please set either USE_LIGHTNING_MODE OR USE_BROWSER_MODE to true (not both) - see updated config-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            // no longer needed
            //if (Settings.SleepBetweenBattles < 4 && Settings.LightningMode && !Settings.ShowBattleResults)
            //{
            //    Log.WriteToLog("Lightning Mode without SHOW_BATTLE_RESULTS enabled - setting minimum sleep time to 4 minutes.", Log.LogType.Warning);
            //    Settings.SleepBetweenBattles = 4;
            //}

            Log.WriteToLog("Config loaded!", Log.LogType.Success);
            Log.WriteToLog($"Config parameters:{Environment.NewLine}" +
                $"MODE: {(Settings.LightningMode ? "LIGHTNING (blockchain)" : "BROWSER")}{Environment.NewLine}" +
                $"DEBUG: {Settings.DebugMode}{Environment.NewLine}" +
                $"WRITE_LOG_TO_FILE: {Settings.WriteLogToFile}{Environment.NewLine}" +
                $"SHOW_API_RESPONSE: {Settings.ShowAPIResponse}{Environment.NewLine}" +
                $"PRIORITIZE_QUEST: {Settings.PrioritizeQuest}{Environment.NewLine}" +
                $"CLAIM_QUEST_REWARD: {Settings.ClaimQuestReward}{Environment.NewLine}" +
                $"CLAIM_SEASON_REWARD: {Settings.ClaimSeasonReward}{Environment.NewLine}" +
                $"REQUEST_NEW_QUEST: {String.Join(",", Settings.BadQuests)}{Environment.NewLine}" +
                $"DONT_CLAIM_QUEST_NEAR_HIGHER_LEAGUE: {Settings.DontClaimQuestNearHigherLeague}{Environment.NewLine}" +
                $"WAIT_FOR_MISSING_CP_AT_QUEST_CLAIM: {Settings.WaitForMissingCPAtQuestClaim}{Environment.NewLine}" +
                $"ADVANCE_LEAGUE: {Settings.AdvanceLeague}{Environment.NewLine}" +
                $"SLEEP_BETWEEN_BATTLES: {Settings.SleepBetweenBattles}{Environment.NewLine}" +
                $"ECR_THRESHOLD: {Settings.ECRThreshold}{Environment.NewLine}" +
                $"USE_API: {Settings.UseAPI}{Environment.NewLine}" +
                $"USE_PRIVATE_API: {Settings.UsePrivateAPI}");
                
            if (Settings.LightningMode)
            {
                Console.Write($"SHOW_BATTLE_RESULTS: {Settings.ShowBattleResults}{Environment.NewLine}");
                Console.Write($"THREADS: {Settings.Threads}{Environment.NewLine}");
            }
            else
            {
                Console.Write($"HEADLESS: {Settings.Headless}{Environment.NewLine}");
                Console.Write($"MAX_BROWSER_INSTANCES: {Settings.MaxBrowserInstances}{Environment.NewLine}");
            }
            return true;
        }

        static void SetupRentalBot()
        {
            var moduleInstance = Activator.CreateInstanceFrom(Settings.RentalBotDllPath, "Splinterlands_Rental_Bot.RentalBot");
            Settings.RentalBot = moduleInstance;
            MethodInfo mi = moduleInstance.Unwrap().GetType().GetMethod("Setup");
            
            mi.Invoke(moduleInstance.Unwrap(), new object[] { Settings._httpClient, false });
            Settings.RentalBotMethodCheckRentals = moduleInstance.Unwrap().GetType().GetMethod("CheckRentals");
            Settings.RentalBotMethodIsAvailable = moduleInstance.Unwrap().GetType().GetMethod("IsAvailable");
            Settings.RentalBotMethodSetActive = moduleInstance.Unwrap().GetType().GetMethod("SetActive");
            Settings.RentalBotActivated = true;
        }



        static bool ReadMasterAccount()
        {
            Log.WriteToLog("Reading master.txt...");
            string filePathMasterAccount = Settings.StartupPath + @"/config/master.txt";
            if (!File.Exists(filePathMasterAccount))
            {
                Log.WriteToLog("No master.txt in config folder.", Log.LogType.CriticalError);
                return false;
            }
            var infos = File.ReadAllLines(filePathMasterAccount)[0].Split(":");

            if (infos.Length != 3)
            {
                Log.WriteToLog("master.txt is formatted incorrectly, must be username:privatekey:activekey", Log.LogType.CriticalError);
                return false;
            }

            Settings.MasterAccount = new Settings.AccInfo(infos[0], infos[1], infos[2]);

            return true;
        }

        static bool ReadAccounts()
        {
            Log.WriteToLog("Reading accounts.txt...");
            string filePathAccounts = Settings.StartupPath + @"/config/accounts.txt";
            string filePathAccessTokens = Settings.StartupPath + @"/config/access_tokens.txt";
            if (!File.Exists(filePathAccounts))
            {
                Log.WriteToLog("No accounts.txt in config folder - see accounts-example.txt!", Log.LogType.CriticalError);
                return false;
            }

            if (!File.Exists(filePathAccessTokens))
            {
                File.WriteAllText(filePathAccessTokens, "#DO NOT SHARE THESE!" + Environment.NewLine);
            }

            if (Settings.LightningMode)
            {
                Settings.BotInstancesBlockchain = new();
            }
            else
            {
                Settings.BotInstancesBrowser = new();
            }

            string[] accessTokens = File.ReadAllLines(filePathAccessTokens);
            int indexCounter = 0;

            foreach (string loginData in File.ReadAllLines(filePathAccounts))
            {
                if (loginData.Trim().Length == 0 || loginData[0] == '#')
                {
                    continue;
                }
                string[] temp = loginData.Split(':');
                var query = accessTokens.Where(x => x.Split(':')[0] == temp[0]);
                string accessToken = query.Any()? query.First().Split(':')[1] : "";
                
                if (temp.Length == 2)
                {
                    Log.WriteToLog("Account.txt reading error. Must include activation key. Account.txt should have form: username : posting key : activation key");
                    throw new Exception("Account.txt must now include activation key. Account.txt should have form: username : posting key : activation key");
                }
                else if (temp.Length == 3)
                {
                    if (Settings.LightningMode)
                    {
                        Settings.BotInstancesBlockchain.Add(new BotInstanceBlockchain(temp[0].Trim().ToLower(), temp[1].Trim(), accessToken, indexCounter++, activeKey: temp[2].Trim()));
                    }
                    else
                    {
                        Settings.BotInstancesBrowser.Add(new BotInstanceBrowser(temp[0].Trim().ToLower(), temp[1].Trim(), indexCounter++, key: temp[2].Trim()));
                    }
                }
            }

            if ((Settings.BotInstancesBrowser != null && Settings.BotInstancesBrowser.Count > 0) || Settings.BotInstancesBlockchain.Count > 0)
            {
                Log.WriteToLog($"Loaded {(Settings.BrowserMode ? Settings.BotInstancesBrowser.Count.ToString().Pastel(Color.Red) : Settings.BotInstancesBlockchain.Count.ToString().Pastel(Color.Red))} accounts!", Log.LogType.Success);
                return true;
            }
            else
            {
                Log.WriteToLog($"Did not load any account", Log.LogType.CriticalError);
                return false;
            }
        }

        static void Initialize()
        {
            if (Settings.BrowserMode && Settings.MaxBrowserInstances > Settings.BotInstancesBrowser.Count)
            {
                Log.WriteToLog($"MAX_BROWSER_INSTANCES is larger than total number of accounts, reducing it to {Settings.BotInstancesBrowser.Count.ToString().Pastel(Color.Red)}", Log.LogType.Warning);
                Settings.MaxBrowserInstances = Settings.BotInstancesBrowser.Count;
            } else if (Settings.LightningMode && Settings.Threads > Settings.BotInstancesBlockchain.Count)
            {
                Log.WriteToLog($"THREADS is larger than total number of accounts, reducing it to {Settings.BotInstancesBlockchain.Count.ToString().Pastel(Color.Red)}", Log.LogType.Warning);
                Settings.Threads = Settings.BotInstancesBlockchain.Count;
            }

            if (Settings.BrowserMode)
            {
                Settings.SeleniumInstances = new List<(OpenQA.Selenium.IWebDriver driver, bool isAvailable)>();
                Log.WriteToLog($"Creating {Settings.MaxBrowserInstances.ToString().Pastel(Color.Red)} browser instances...");
                for (int i = 0; i < Settings.MaxBrowserInstances; i++)
                {
                    Settings.SeleniumInstances.Add((SeleniumAddons.CreateSeleniumInstance(disableImages: false), true));
                    Thread.Sleep(1000);
                }
                Log.WriteToLog("Browser instances created!", Log.LogType.Success);
            }

            Settings.QuestTypes = new Dictionary<string, string>
            {
                { "Defend the Borders", "life" },
                { "Pirate Attacks", "water" },
                { "High Priority Targets", "snipe" },
                { "Lyanna's Call", "earth" },
                { "Stir the Volcano", "fire" },
                { "Rising Dead", "death" },
                { "Stubborn Mercenaries", "neutral" },
                { "Gloridax Revenge", "dragon" },
                { "Stealth Mission", "sneak" }
            };

            Settings.CardsDetails = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Settings.StartupPath + @"/data/cardsDetails.json"));

            Settings.Summoners = new Dictionary<string, string>
            {
                { "260", "fire" },
                { "257", "water" },
                { "437", "water" },
                { "224", "dragon" },
                { "189", "earth" },
                { "145", "death" },
                { "240", "dragon" },
                { "167", "fire" },
                { "438", "death" },
                { "156", "life" },
                { "440", "fire" },
                { "114", "dragon" },
                { "441", "life" },
                { "439", "earth" },
                { "262", "dragon" },
                { "261", "life" },
                { "178", "water" },
                { "258", "death" },
                { "27", "earth" },
                { "38", "life" },
                { "49", "death" },
                { "5", "fire" },
                { "70", "fire" },
                { "73", "life" },
                { "259", "earth" },
                { "74", "death" },
                { "72", "earth" },
                { "442", "dragon" },
                { "71", "water" },
                { "88", "dragon" },
                { "78", "dragon" },
                { "200", "dragon" },
                { "16", "water" },
                { "239", "life" },
                { "254", "water" },
                { "235", "death" },
                { "113", "life" },
                { "109", "death" },
                { "110", "fire" },
                { "291", "dragon" },
                { "278", "earth" },
                { "236", "fire" },
                { "56", "dragon" },
                { "112", "earth" },
                { "111", "water" },
                { "205", "dragon" },
                { "130", "dragon" }
            };

            Settings.LogSummaryList = new List<(int index, string account, string battleResult, string rating, string ECR, string questStatus)>();

            if (Settings.LightningMode)
            {
                Settings.oHived = new HiveAPI.CS.CHived(Settings._httpClient, "https://api.deathwing.me");
            }
        }

        static void SetStartupPath()
        {
            // Setup startup path
            string path = Assembly.GetExecutingAssembly().Location;
            string directory = Path.GetDirectoryName(path);
            Settings.StartupPath = directory;
        }
        static bool CheckForChromeDriver()
        {
            var chromeDriverFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "chromedriver.exe" : "chromedriver";
            if (!File.Exists(Settings.StartupPath + @"/" + chromeDriverFileName))
            {
                Log.WriteToLog("No ChromeDriver installed - please download from https://chromedriver.chromium.org/ and insert .exe into bot folder or use lightning mode", Log.LogType.CriticalError);
                return false;
            }

            return true;
        }


        static bool ReadTransferConfig()
        {

            if (!File.Exists(Settings.POWER_STICK_CONFIG))
            {
                Log.WriteToLog("Could not find file: cards_to_trade.txt");
                return false;
            }


            var AllCards = new List<Card>();
            var lines = File.ReadAllLines(Settings.POWER_STICK_CONFIG);

            foreach (string line in lines)
            {
                var split = line.Split(":");
                if (split.Length != 4)
                {
                    Log.WriteToLog("card_to_trade.txt contains an invalid line. Must be of type Card Generic ID : UUID : Golden : Edition");
                    return false;
                }
                AllCards.Add(new Card(split[0], split[1], null, bool.Parse(split[2]), split[3]));

            }

            Settings.PowerStickCards = AllCards;
            return true;
        }

        static bool ConsoleEventCallback(int eventType)
        {
            if (Settings.BrowserMode && eventType == 2)
            {
#pragma warning disable CS1998
                _ = Task.Run(async () => Parallel.ForEach(Settings.SeleniumInstances, x => x.driver.Quit())).ConfigureAwait(false);
#pragma warning restore CS1998
                Log.WriteToLog("Closing browsers...");
                System.Threading.Thread.Sleep(4500);
            }
            return false;
        }
        static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
    }
}
