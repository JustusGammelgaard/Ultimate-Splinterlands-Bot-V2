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
    public struct CardInfo
    {
        public string GenericID;
        public string UUID;
        public bool Golden;
        public string edition;

        public CardInfo(string GenericID, string UUID, bool Golden, string edition) : this()
        {
            this.GenericID = GenericID;
            this.UUID = UUID;
            this.Golden = Golden;
            this.edition = edition;
        }
    }
    public class CardsToTrade
    {

        // Should be written as; "Card Generic ID" : "UUID" : "Golden" : "Edition"
        public List<CardInfo> AllCards;
        public string CARDTOTRADEFILE = "config/cards_to_trade.txt";

        public CardsToTrade()
        {
            AllCards = ReadCards();
        }

        public List<CardInfo> ReadCards()
        {
            if (!File.Exists(CARDTOTRADEFILE))
                throw new Exception("Could not find file: cards_to_trade.txt");

            AllCards = new List<CardInfo>();    
            var lines = File.ReadAllLines(CARDTOTRADEFILE);

            foreach(string line in lines)
            {
                var split = line.Split(":");
                if (split.Length != 4)
                    throw new Exception("card_to_trade.txt contains an invalid line. Must be of type Card Generic ID : UUID : Golden : Edition");
                AllCards.Add(new CardInfo(split[0], split[1], bool.Parse(split[2]), split[3]));

            }
            return AllCards;
            
        }

    }  
    public class HTMLTransferCards
    {
        public IWebDriver driver;
        private string username;
        private string postingkey;
        public CardsToTrade cardInfos;
        private string activationkey;
        private string TRANSFER_LOG = @"/transfer_log.txt";


        public HTMLTransferCards(string username, string postingkey, string activationkey, CardsToTrade cardInfos)
        {
            this.username = username;
            this.postingkey = postingkey;
            this.cardInfos = cardInfos;
            this.activationkey = activationkey;
        }

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


        private static IWebDriver SetDriver()
        {
            Log.WriteToLog("Starting transfer of cards ... ");
            var options = new ChromeOptions();
            options.AddArgument("--disable-notifications");
            options.AddArgument("--mute-audio");
            options.AddArgument("--log-level=3");


            options.AddArgument("--window-size=1920,1080"); // Important for headless mode
            options.AddArgument("--ignore-certificate-errors"); // Important for headless mode

            options.AddArgument("--headless"); // Does not start UI
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--remote-debugging-port=9222");




            var driver = new ChromeDriver(Environment.CurrentDirectory, options);

            return driver;
        }

        // Testing method - use if crashes happen (screen shots the headless chrome screen).
        public void ScreenShot(string outfile = "driverscreenshot.png")
        {
            Screenshot ss = ((ITakesScreenshot)driver).GetScreenshot();
            ss.SaveAsFile(outfile, ScreenshotImageFormat.Png);
        }

        public void LoginForTrade()
        {
            driver = SetDriver();
            driver.Navigate().GoToUrl("https://splinterlands.com/?p=card_details&id=331&gold=false&edition=3&tab="); // Goto some random card.
            Thread.Sleep(4000); // Sleep for page to load
            driver.FindElement(By.Id("log_in_button")).Click(); // Press login
            Thread.Sleep(3000); // Wait for login screen

            var usernameslot = driver.FindElement(By.Id("email"));
            var passwordslot = driver.FindElement(By.Id("password"));

            usernameslot.SendKeys(username); // Fill in details
            Thread.Sleep(500);

            passwordslot.SendKeys(postingkey);// Fill in details
            Thread.Sleep(1500);

            driver.FindElement(By.XPath("//button[@name='loginBtn' and @class='btn btn-primary btn-lg']")).Click(); // Press login
            Thread.Sleep(2000);


        }

        private void RemovePopups()
        {
            try
            {
                driver.ExecuteJavaScript("$('.modal').modal('hide');", suppressErrors: true); // Remove pop-ups
                Thread.Sleep(3000);
            }
            catch (Exception ex)
            {
                Thread.Sleep(1000);
            }
        }

        private void GoToTradeURL(string cardGenericID, bool golden, string edition)
        {
            driver.Navigate().GoToUrl("https://splinterlands.com/?p=card_details&id=" + cardGenericID + "&gold=" + golden.ToString().ToLower() + "&edition=" + edition + "&tab="); // Goto card exchange
            Thread.Sleep(4000);



        }

        // Assumes that we are at the correct URL.
        private void TradeOneCard(string UUID, string toUser)
        {
            driver.FindElement(By.XPath("//*[@card_id='" + UUID + "' and @class='card-checkbox']")).Click(); // Press checkbox of card
            Thread.Sleep(100 * Settings._Random.Next(10, 20)); // 1-2 sec

            driver.FindElement(By.XPath("//*[@id='btn_send' and @class='send enabled']")).Click(); // Press transfer
            Thread.Sleep(1000 * Settings._Random.Next(5, 8));

            driver.FindElement(By.Id("recipient")).SendKeys(toUser); // Insert name
            Thread.Sleep(2000);

            driver.FindElement(By.XPath("//button[@id='btn_send_popup_send' and @class='new-button green']")).Click(); // Press send
            Thread.Sleep(7000);

            driver.FindElement(By.Id("active_key")).SendKeys(activationkey); // Insert activation key to complete.
            Thread.Sleep(3000);

            driver.FindElement(By.XPath("//button[@id='approve_tx' and @class='gradient-button green']")).Click(); // Press approve
            Thread.Sleep(1000 * Settings._Random.Next(10, 15)); // Sleep a bit, sending cards may take some time.
        }

        // toUser = recipient username.
        public async Task<bool> TradeAllCardsAsync(string toUser)
        {
            try
            {

                var mustBeTransfered = await FindCardsToTransferAsync();


                if (mustBeTransfered.Count == 0)
                {
                    Log.WriteToLog("No cards could be traded." + Environment.NewLine, logFile: TRANSFER_LOG);
                    return true;
                }
                LoginForTrade();

                foreach (CardInfo CI in mustBeTransfered)
                {
                    GoToTradeURL(CI.GenericID, CI.Golden, CI.edition);
                    RemovePopups();

                    TradeOneCard(CI.UUID, toUser);

                    Log.WriteToLog("Carded trade from: " + username + " to: " + toUser + ". Card UUID: " + CI.UUID + Environment.NewLine, logFile: TRANSFER_LOG);


                }
                Logout();
                Log.WriteToLog("Succesful traded all cards, from: " + username + " to: " + toUser + "." + Environment.NewLine, logFile: TRANSFER_LOG);
                return true;

            }
            catch (Exception ex)
            {
                Log.WriteToLog("Error in transfer from: " + username + " to: " + toUser + ". Error code: " + ex.ToString() + Environment.NewLine, Log.LogType.Error, logFile: TRANSFER_LOG);

                try
                {
                    Logout();
                }
                catch { }

                return false;
            }

        }
        public async Task<bool> SendCardsToMasterAsync(int masterID = 0)
        {
            Log.WriteToLog("Trading all cards from accounts to master..." + Environment.NewLine, logFile: TRANSFER_LOG);



            try
            {
                var bots = Settings.BotInstancesBlockchain;
                var masterBotInfo = bots[masterID];

                for (int i = 0; i < bots.Count; i++)
                {
                    if (i == masterID)
                        continue;

                    var botinfo = bots[i];

                    this.username = botinfo.Username;
                    this.postingkey = botinfo.PostingKey;
                    this.activationkey = botinfo.ActiveKey;

                    var mustBeTransfered = await FindCardsToTransferAsync();

                    // No cards should be transfered.
                    if (mustBeTransfered.Count() == 0) continue;

                    LoginForTrade();
                    foreach (CardInfo CI in mustBeTransfered)
                    {
                        GoToTradeURL(CI.GenericID, CI.Golden, CI.edition);
                        RemovePopups();

                        // Check if the card is actually there. (This should never be true, but just in case)
                        var cardNotFound = driver.FindElement(By.XPath("//button[@data-dismiss='modal' and @class='new-button']"));
                        if (cardNotFound.Location != new Point(0, 0))
                            continue;


                        TradeOneCard(CI.UUID, masterBotInfo.Username);
                        Log.WriteToLog("Carded trade from: " + username + " to: " + masterBotInfo.Username + ". Card UUID: " + CI.UUID + Environment.NewLine, logFile: TRANSFER_LOG);
                    }
                    Logout();

                }
                Log.WriteToLog("All cards traded to master." + Environment.NewLine, logFile: TRANSFER_LOG);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteToLog("Failure in transfering cards to master. Error: " + ex.ToString() + Environment.NewLine, Log.LogType.Error, logFile: TRANSFER_LOG);
                try
                {
                    Logout();
                }
                catch { }

                return false;
            }

        }


        public void Logout()
        {
            Log.WriteToLog("Cards transfered.");

            driver.Quit();
        }


        private async Task<List<CardInfo>> FindCardsToTransferAsync()
        {
            // Gets all cards of an account
            var CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(this.username, false, false);

            // These cards should be transfered back to master
            var mustBeTransfered = new List<CardInfo>();


            // Check if the account has any card which should be transfered.
            foreach (var c in cardInfos.AllCards)
            {
                if (CardsCached.Where(cachedcard => cachedcard.card_long_id == c.UUID).Count() > 0)
                    mustBeTransfered.Add(c);
            }

            return mustBeTransfered;
        }



    }
}
