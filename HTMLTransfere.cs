using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
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
        private string TRANSFER_LOG = "transfer_log.txt";

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

                if (botECR > maxECR && botECR >= Settings.ECRThreshold)
                {
                    maxECR = botECR;
                    maxBot = Settings.BotInstancesBlockchain.IndexOf(bot);
                }
            }
            return maxBot;
        }


        private static IWebDriver SetDriver()
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-notifications");

            var driver = new ChromeDriver(Environment.CurrentDirectory, options);

            return driver;
        }

        private void Login()
        {
            driver = SetDriver();
            Thread.Sleep(2500);
            driver.Navigate().GoToUrl("https://www.splinterlands.com/"); // Go to splinterlands
            Thread.Sleep(5000); // Sleep for page to load
            driver.FindElement(By.Id("log_in_button")).Click(); // Press login
            Thread.Sleep(3000); // Wait for login screen

            var usernameslot = driver.FindElement(By.Id("email"));
            var passwordslot = driver.FindElement(By.Id("password"));

            usernameslot.SendKeys(username); // Fill in details
            Thread.Sleep(500);

            passwordslot.SendKeys(postingkey);// Fill in details
            Thread.Sleep(1500);

            driver.FindElement(By.XPath("//button[@name='loginBtn' and @class='btn btn-primary btn-lg']")).Click(); // Press login
            Thread.Sleep(5000);
        }

        private void RemovePopups()
        {
            try
            {
                driver.ExecuteJavaScript("$('.modal').modal('hide');", suppressErrors: true); // Remove pop-ups
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                Thread.Sleep(1000);
            }
        }

        private void GoToTradeURL(string cardGenericID, bool golden, string edition)
        {
            driver.Navigate().GoToUrl("https://splinterlands.com/?p=card_details&id=" + cardGenericID + "&gold=" + golden.ToString().ToLower() + "&edition=" + edition + "&tab="); // Goto card exchange
            Thread.Sleep(8000);
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
            Thread.Sleep(9000);

            driver.FindElement(By.Id("active_key")).SendKeys(activationkey); // Insert activation key to complete.
            Thread.Sleep(3000);

            driver.FindElement(By.XPath("//button[@id='approve_tx' and @class='gradient-button green']")).Click(); // Press approve
            Thread.Sleep(1000 * Settings._Random.Next(10, 15)); // Sleep a bit, sending cards may take some time.
        }

        // toUser = recipient username.
        public bool TradeAllCards(string toUser)
        {
            try
            {

                Login();

                foreach (CardInfo CI in cardInfos.AllCards)
                {
                    GoToTradeURL(CI.GenericID, CI.Golden, CI.edition);
                    RemovePopups();

                    TradeOneCard(CI.UUID, toUser);

                    File.AppendAllText(TRANSFER_LOG, "Carded trade from: " + username + " to: " + toUser + ". Card UUID: "+ CI.UUID + Environment.NewLine);


                }
                Logout();

                File.AppendAllText(TRANSFER_LOG, "Succesful traded all cards, from: " + username + " to: " + toUser + "." + Environment.NewLine);
                return true;

            }
            catch (Exception ex)
            {
                File.AppendAllText(TRANSFER_LOG, "Error in transfer from: " + username + " to: " + toUser + ". Error code: " + ex.ToString() + Environment.NewLine);
                return false;
            }

        }
        // Fairly time consuming - use infrequently!
        public async Task<bool> SendCardsToMasterAsync(int masterID = 0)
        {
            File.AppendAllText(TRANSFER_LOG, "Trading all cards from accounts to master..." + Environment.NewLine);


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

                    // Gets all cards of an account
                    var CardsCached = await SplinterlandsAPI.GetPlayerCardsAsync(this.username, false);

                    // These cards should be transfered back to master
                    var mustBeTransfered = new List<CardInfo>();


                    // Check if the account has any card which should be transfered.
                    foreach (var c in cardInfos.AllCards)
                    {
                        if (CardsCached.Where(cachedcard => cachedcard.card_long_id == c.UUID).Count() > 0)
                            mustBeTransfered.Add(c);
                    }

                    // No cards should be transfered.
                    if (mustBeTransfered.Count() == 0) continue;

                    Login();

                    foreach (CardInfo CI in mustBeTransfered)
                    {
                        GoToTradeURL(CI.GenericID, CI.Golden, CI.edition);
                        RemovePopups();

                        // Check if the card is actually there. (This should never be true, but just in case)
                        var cardNotFound = driver.FindElement(By.XPath("//button[@data-dismiss='modal' and @class='new-button']"));
                        if (cardNotFound.Location != new Point(0, 0))
                            continue;


                        TradeOneCard(CI.UUID, masterBotInfo.Username);

                        File.AppendAllText(TRANSFER_LOG, "Carded trade from: " + username + " to: " + masterBotInfo.Username + ". Card UUID: " + CI.UUID + Environment.NewLine);
                    }
                    Logout();

                }

                File.AppendAllText(TRANSFER_LOG, "All cards traded to master." + Environment.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                File.AppendAllText(TRANSFER_LOG, "Failure in transfering cards to master. Error: " + ex.ToString() + Environment.NewLine);
                return false;
            }

        }


        private void Logout()
        {
            driver.Quit();
        }



    }
}
