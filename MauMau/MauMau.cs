using LamestWebserver;
using LamestWebserver.UI;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Demos
{
    internal enum Suit
    {
        Kreuz, Pik, Karo, Herz
    }

    internal enum Face
    {
        _7, _8, _9, _10, Bube, Dame, Koenig, Ass
    }

    internal struct Card
    {
        internal readonly Suit suit;
        internal readonly Face face;
        internal Suit displayedSuit;

        internal Card(Suit suit, Face face)
        {
            displayedSuit = this.suit = suit;
            this.face = face;
        }

        private HElement ToButtonInternal(int? index, string className, string additionalParams = "")
        {
            return new HButton("", index.HasValue ? $"?play={index.Value}{additionalParams}" : "")
            {
                Class = $"{className}{(index.HasValue ? " playable" : "")}",
                Elements =
                    {
                        new HContainer { Class = $"suit {displayedSuit}" },
                        new HContainer { Class = $"face {face}" }
                    }
            };
        }

        public HElement ToButton(int? index)
        {
            if (face != Face.Bube || !index.HasValue)
            {
                return ToButtonInternal(index, "card");
            }
            else
            {
                return new HContainer()
                {
                    Class = "card Bube",
                    Elements = 
                    {
                        new Card(suit, face){ displayedSuit = Suit.Kreuz }.ToButtonInternal(index, "subcard", $"&suit={Suit.Kreuz}"),
                        new Card(suit, face){ displayedSuit = Suit.Pik }.ToButtonInternal(index, "subcard", $"&suit={Suit.Pik}"),
                        new Card(suit, face){ displayedSuit = Suit.Karo }.ToButtonInternal(index, "subcard", $"&suit={Suit.Karo}"),
                        new Card(suit, face){ displayedSuit = Suit.Herz }.ToButtonInternal(index, "subcard", $"&suit={Suit.Herz}"),
                    }
                };
            }
        }
    }

    internal class CardState
    {
        public ulong cardsLeft;
        public int cardCount;

        public CardState(List<Card> cards)
        {
            foreach (var c in cards)
                AddCard(c.suit, c.face);
        }

        public CardState(Player p, Card firstCard)
        {
            cardsLeft = (ulong)0xFFFFFFFF;
            cardCount = 32;

            foreach (var c in p.cards)
                RemoveCard(c.suit, c.face);

            RemoveCard(firstCard.suit, firstCard.face);
        }

        public bool ContainsCard(Suit s, Face f)
        {
            return 0 != ((cardsLeft >> (int)(((int)s * Enum.GetValues(typeof(Face)).Length) + (int)f)) & 1);
        }

        internal bool ContainsCard(Card card) => ContainsCard(card.suit, card.face);

        public void RemoveCard(Suit s, Face f)
        {
            cardsLeft &= ~((ulong)1 << (int)(((int)s * Enum.GetValues(typeof(Face)).Length) + (int)f));
            cardCount--;
        }

        public void RemoveCard(Card c) => RemoveCard(c.suit, c.face);

        public void AddCard(Suit s, Face f)
        {
            cardsLeft |= ((ulong)1 << (int)(((int)s * Enum.GetValues(typeof(Face)).Length) + (int)f));
            cardCount++;
        }

        public int GetSuitCards(Suit s)
        {
            ulong suitMask = (((ulong)1 << Enum.GetValues(typeof(Face)).Length) - 1) << ((int)s * Enum.GetValues(typeof(Face)).Length);

            return PopCnt(cardsLeft & suitMask);
        }

        public static ulong GetSuitMask(Suit s) => (((ulong)1 << Enum.GetValues(typeof(Face)).Length) - 1) << ((int)s * Enum.GetValues(typeof(Face)).Length);

        public int GetFaceCards(Face f)
        {
            ulong faceMask = (ulong)(1 << (int)f) * (ulong)(0x99); // relies on number of faces being 8.

            return PopCnt(cardsLeft & faceMask);
        }

        public static ulong GetFaceMask(Face f) => (ulong)(1 << (int)f) * (ulong)(0x99); // relies on number of faces being 8.

        public int GetMatchingCardCount(ulong mask) => PopCnt(cardsLeft & mask);

        // https://stackoverflow.com/a/11517887 ????
        private static byte PopCnt(ulong value)
        {
            ulong result = value - ((value >> 1) & 0x5555555555555555UL);
            result = (result & 0x3333333333333333UL) + ((result >> 2) & 0x3333333333333333UL);
            return (byte)(unchecked(((result + (result >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }
    }

    internal class PlayerInformation
    {
        public Dictionary<CardState, int> cardsFromCardState = new Dictionary<CardState, int>();
    }

    internal class Player
    {
        public List<Card> cards = new List<Card>();
        public List<CardState> cardStates = new List<CardState>();
        public Dictionary<Player, PlayerInformation> otherPlayerInformation = new Dictionary<Player, PlayerInformation>();
        public int botType = 0;

        public void Init(IEnumerable<Player> players)
        {
            foreach (Player p in players)
                if (p != this)
                    otherPlayerInformation.Add(p, new PlayerInformation());
        }

        public void NotifyCardShuffle(List<Card> cards)
        {
            var cardState = new CardState(cards);

            cardStates.Add(cardState);

            foreach (var p in otherPlayerInformation)
                p.Value.cardsFromCardState.Add(cardState, 0);
        }

        public void NotifyPlayerDrawCard(Player player)
        {
            if (player == this)
                return;

            // Add card to other player count per current set.
            otherPlayerInformation[player].cardsFromCardState[cardStates.Last()]++;
        }

        public void NotifySelfDrawCard(Card card)
        {
            cards.Add(card);
            cardStates.Last().RemoveCard(card);
        }

        public void NotifyOtherPlayerCardPlayed(Card card, Player player)
        {
            var cardState = (from c in cardStates where c.ContainsCard(card) select c).First();

            // Remove card count from other player.
            if (player != null)
                otherPlayerInformation[player].cardsFromCardState[cardState]--;

            cardState.RemoveCard(card);
        }
    }

    internal class GameState
    {
        public LamestWebserver.Collections.AVLTree<string, Player> players = new LamestWebserver.Collections.AVLTree<string, Player>();
        public List<Card> playedCards = new List<Card>();
        public List<Card> availableCards = new List<Card>();
        public bool gameStarted = false;
        public bool isFirstTurn = true;
        public bool lastTurnWasDraw = false;
        public int playerTurnIndex = 0;
        public int sevenDrawCounter = 0;

        public static string EasyBotName = "DracoMalfoy";

        public bool IsCardValidTurn(Card card)
        {
            if (card.suit == playedCards.LastOrDefault().displayedSuit || card.face == playedCards.LastOrDefault().face || card.face == Face.Bube)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Draw a card for a given player.
        /// </summary>
        /// <param name="drawingPlayer">Null if this is the first card drawn</param>
        public void DrawCard(Player drawingPlayer)
        {
            // Attention: WE RELY ON THE FACT THAT THE CARD STATE IS REPLENISHED WHENEVER THE SET WAS EMPTY! Do not try to replenish the cards when there's no card left AFTER drawing!
            if (availableCards.Count == 0)
            {
                Card lastPlayedCard = playedCards.LastOrDefault();
                playedCards.RemoveAt(playedCards.Count - 1);

                var tmp = availableCards;
                availableCards = playedCards;
                playedCards = tmp;

                Shuffle(ref availableCards);

                playedCards.Add(lastPlayedCard);

                foreach (var x in players.Values)
                    x.NotifyCardShuffle(availableCards);
            }

            var card = availableCards[0];
            availableCards.RemoveAt(0);

            if (drawingPlayer == null) // first ever Card gets drawn.
                playedCards.Add(card);
            else
                drawingPlayer.NotifySelfDrawCard(card);

            foreach (var p in players)
            {
                if (p.Value != drawingPlayer)
                {
                    if (drawingPlayer != null)
                        p.Value.NotifyPlayerDrawCard(drawingPlayer);
                    else
                        p.Value.NotifyOtherPlayerCardPlayed(card, null);
                }
            }
        }

        public void Shuffle(ref List<Card> cards)
        {
            cards = (from c in cards select new Card(c.suit, c.face)).ToList(); // Changing the `displayedSuit` back to `suit`.

            Random rand = new Random();

            for (int i = 0; i < 1000; i++)
            {
                int a = rand.Next(0, cards.Count - 1);
                int b = rand.Next(0, cards.Count - 1);

                Card tmp = cards[b];
                cards[b] = cards[a];
                cards[a] = tmp;
            }
        }

        public void EndTurn()
        {
            playerTurnIndex = (playerTurnIndex + 1) % players.Count;
            isFirstTurn = true;
            lastTurnWasDraw = false;
        }

        public void StartGame(List<string> lobby)
        {
            playedCards = new List<Card>();
            availableCards = new List<Card>();
            playerTurnIndex = 0;
            sevenDrawCounter = 0;
            isFirstTurn = true;
            players = new LamestWebserver.Collections.AVLTree<string, Player>();

            foreach (var l in lobby)
            {
                players.Add(l, new Player()); 

                if (l == EasyBotName)
                    players.Last().Value.botType = 1; // Mark bot name players as bots.
            }

            foreach (var suit in Enum.GetValues(typeof(Suit)))
                foreach (var face in Enum.GetValues(typeof(Face)))
                    availableCards.Add(new Card((Suit)suit, (Face)face));

            Shuffle(ref availableCards);

            foreach (var p in players.Values)
            {
                p.Init(players.Values);
                p.NotifyCardShuffle(availableCards);
            }

            // First card:
            DrawCard(null);

            foreach (var p in players.Values)
                for (int i = 0; i < 5; i++)
                    DrawCard(p);

            gameStarted = true;
        }

        public bool PlayCard(int cardIndex, string suit)
        {
            int initialPlayerTurnIndex = playerTurnIndex;
            Player currentPlayer = players[players.Keys.ToArray()[playerTurnIndex]];
            List<Card> cards = currentPlayer.cards;

            Card card = cards[cardIndex];
            bool validCard = true;

            switch (card.face)
            {
                case Face.Bube:
                    {
                        if (Enum.TryParse(suit, out Suit chosenSuit))
                            card.displayedSuit = chosenSuit;
                        else
                            validCard = false;

                        break;
                    }
            }

            if (validCard)
            {
                if (isFirstTurn && playedCards.LastOrDefault().face == Face._7 && card.face != Face._7 && sevenDrawCounter > 0)
                {
                    for (int i = 0; i < sevenDrawCounter; i++)
                        DrawCard(currentPlayer);

                    sevenDrawCounter = 0;
                    lastTurnWasDraw = true;
                }

                isFirstTurn = false;
                lastTurnWasDraw = false;
                playedCards.Add(card);
                cards.RemoveAt(cardIndex);

                foreach (var p in players)
                    if (p.Value != currentPlayer)
                        p.Value.NotifyOtherPlayerCardPlayed(playedCards.Last(), currentPlayer);

                switch (card.face)
                {
                    case Face.Ass:
                        break;

                    case Face._8:
                        playerTurnIndex++;
                        EndTurn();
                        break;

                    case Face._7:
                        sevenDrawCounter += 2;
                        EndTurn();
                        break;

                    default:
                        EndTurn();
                        break;
                }

                if (cards.Count == 0 && playerTurnIndex != initialPlayerTurnIndex) // you're out.
                {
                    if (playerTurnIndex > initialPlayerTurnIndex)
                        playerTurnIndex--;

                    players.Remove(players.Keys.ToList()[initialPlayerTurnIndex]); // this is bad. still relying on keys.ToList() to always be in the same order, but now we may even know that it is in fact the same order, since it's ... ordered. thanks avl-tree!

                    if (players.Count <= 1)
                        gameStarted = false;
                }

                return true;
            }

            return false;
        }
    }

    internal class MauMau : PageResponse
    {
        List<string> lobby = new List<string>();
        GameState gameState = new GameState();
        LamestWebserver.Synchronization.UsableLockSimple mutex = new LamestWebserver.Synchronization.UsableLockSimple();

        public MauMau() : base(nameof(MauMau)) { }

        protected override string GetContents(SessionData sessionData)
        {
            using (mutex.Lock())
            {
                // Get the default layout around the elements retrieved by GetElements()
                HElement page = MainPage.GetPage(GetElements(sessionData as HttpSessionData), "MauMau");

                // Handle Bots.
                if (gameState.gameStarted)
                {
                    while (true)
                    {
                        var player = gameState.players[gameState.players.Keys.ToList()[gameState.playerTurnIndex]];

                        if (player.botType == 0)
                            break;

                        switch (player.botType)
                        {
                            case 1:
                                HandleEasyBot(gameState, player);
                                break;
                        }
                    }
                }

                // To get the HTML-string of an HElement, call GetContent with the current session data.
                return page.GetContent(sessionData);
            }
        }

        private IEnumerable<HElement> GetElements(HttpSessionData sessionData)
        {
            if (!sessionData.KnownUser)
            {
                string username = sessionData.HttpPostVariables["username"];

                if (!string.IsNullOrEmpty(username))
                {
                    sessionData.RegisterUser(username);
                    lobby.Add(username);
                    yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 0);
                    yield break;
                }
                
                yield return new HHeadline("MauMau Login");
                yield return new HForm(nameof(MauMau)) { Elements = 
                    {
                        new HInput(HInput.EInputType.text, "username"),
                        new HButton("Login", HButton.EButtonType.submit)
                    } };

                yield break;
            }
            
            yield return new HHeadline($"Hello {sessionData.UserName}!");

            if (!gameState.gameStarted)
            {
                if (null != sessionData.HttpHeadVariables["start"])
                {
                    gameState.StartGame(lobby);
                    yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 0);
                    yield break;
                }

                // Add bot.
                if (null != sessionData.HttpHeadVariables["bot"])
                {
                    if (!lobby.Contains(GameState.EasyBotName))
                        lobby.Add(GameState.EasyBotName);
                }

                yield return new HHeadline("Lobby:", 2);
                yield return new HList(HList.EListType.UnorderedList, from x in lobby select new HText(x));

                if (lobby.Count > 1)
                    yield return new HLink("Start Game.", $"{nameof(MauMau)}?start");

                yield return new HLink("Add Bot.", $"{nameof(MauMau)}?bot");

                yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 1000);
                yield break;
            }

            // if we're spectating.
            if (!gameState.players.ContainsKey(sessionData.UserName))
            {
                yield return new HContainer { Class = $"active_card{(gameState.playedCards.LastOrDefault().face == Face._7 && gameState.sevenDrawCounter > 0 ? $" draw _{gameState.sevenDrawCounter}" : "")}", Elements = { gameState.playedCards.LastOrDefault().ToButton(null) } };

                yield return new HList(HList.EListType.OrderedList, from p in gameState.players.OrderBy(x => gameState.players[x.Key].cards.Count) select new HContainer { Elements = { new HText($"{p.Key}: {gameState.players[p.Key].cards.Count} Cards"), new HContainer { Class = "player_preview", Elements = (from c in gameState.players[p.Key].cards select c.ToButton(null)).ToList() } } });

                yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 1000);
                yield break;
            }

            yield return new HContainer { Class = $"active_card{(gameState.playedCards.LastOrDefault().face == Face._7 && gameState.sevenDrawCounter > 0 ? $" draw _{gameState.sevenDrawCounter}" : "")}", Elements = { gameState.playedCards.LastOrDefault().ToButton(null) } };

            yield return new HList(HList.EListType.UnorderedList, from p in gameState.players where p.Key != sessionData.UserName select (HElement)new HText($"{p.Key}: {gameState.players[p.Key].cards.Count}")) { Class = "other_players" };

            yield return new HText($"Draw Pile: {gameState.availableCards.Count}") { Class = "available_cards" };

            yield return new HHeadline("Your Cards:", 2);

            var currentPlayer = gameState.players[sessionData.UserName];

            // not sure if ToList returns the Keys in the correct order for every functioncall.
            if (gameState.players.Keys.ToList()[gameState.playerTurnIndex] != sessionData.UserName) // if it's currently someone elses turn.
            {
                yield return new HContainer { Class = "your_cards", Elements = (from x in currentPlayer.cards select x.ToButton(null)).ToList() };
                yield return new HScript(ScriptCollection.GetPageReferalToXInMilliseconds, nameof(MauMau), 1000);
            }
            else // if it's currently our turn.
            {
                var cards = currentPlayer.cards;
                string playString = sessionData.HttpHeadVariables["play"];

                if (int.TryParse(playString, out int playIndex) && gameState.IsCardValidTurn(cards[playIndex]))
                {
                    if (gameState.PlayCard(playIndex, sessionData.HttpHeadVariables["suit"]))
                    {
                        yield return new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau));
                        yield break;
                    }
                }

                if (null != sessionData.HttpHeadVariables["draw"] && !gameState.lastTurnWasDraw)
                {
                    if (gameState.isFirstTurn && gameState.playedCards.LastOrDefault().face == Face._7 && gameState.sevenDrawCounter > 0)
                    {
                        for (int i = 0; i < gameState.sevenDrawCounter; i++)
                            gameState.DrawCard(currentPlayer);

                        gameState.sevenDrawCounter = 0;
                        gameState.isFirstTurn = false;
                        gameState.lastTurnWasDraw = true;
                    }
                    else
                    {
                        gameState.DrawCard(currentPlayer);
                        gameState.EndTurn();

                        yield return new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau));
                        yield break;
                    }
                }
                else if (null != sessionData.HttpHeadVariables["end"])
                {
                    gameState.EndTurn();
                }

                bool anyValidCard = false;

                for (int i = 0; i < cards.Count; i++)
                {
                    bool isCardValid = gameState.IsCardValidTurn(cards[i]);
                    anyValidCard |= isCardValid;

                    yield return cards[i].ToButton(isCardValid ? i : (int?)null);
                }

                if (gameState.lastTurnWasDraw)
                {
                    if (anyValidCard)
                    {
                        yield return new HButton("End Turn", "?end") { Class = "action" };
                    }
                    else
                    {
                        gameState.EndTurn();

                        yield return new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau));
                        yield break;
                    }
                }
                else
                {
                    yield return new HButton("Draw", "?draw") { Class = "action" };
                }

                // Cheats.
                if (anyValidCard)
                {
                    List<IEnumerable<HElement>> table = new List<IEnumerable<HElement>>();

                    table.Add(new List<HElement>() { "Card", "Probability" });

                    var nextPlayer = gameState.players[gameState.players.Keys.ToList()[(gameState.playerTurnIndex + 1) % gameState.players.Count]];
                    var player = gameState.players[gameState.players.Keys.ToList()[gameState.playerTurnIndex % gameState.players.Count]];

                    foreach (var c in cards)
                    {
                        if (gameState.IsCardValidTurn(c))
                        {
                            if (c.face == Face.Bube)
                            {
                                foreach (var suit in Enum.GetValues(typeof(Suit)))
                                    AddCardProbabilities(table, player, new Card((Suit)suit, Face.Bube), nextPlayer);
                            }
                            else
                            {
                                AddCardProbabilities(table, player, c, nextPlayer);
                            }
                        }
                    }
                    
                    yield return new HTable(table);
                }
            }
        }

        private void HandleEasyBot(GameState gameState, Player player)
        {
            double lowestProbability = 1;
            double lowest7probability = 1;
            int bestCardIndex = -1;
            int best7Index = -1;
            Suit bubeSuit = Suit.Kreuz;

            var cards = player.cards;
            var nextPlayer = gameState.players[gameState.players.Keys.ToList()[(gameState.playerTurnIndex + 1) % gameState.players.Count]];

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];

                if (gameState.IsCardValidTurn(card))
                {
                    if (card.face == Face.Bube)
                    {
                        foreach (var suit in Enum.GetValues(typeof(Suit)))
                        {
                            var probability = GetProbabilityForCard(player, new Card((Suit)suit, card.face), nextPlayer);

                            if (probability < lowestProbability)
                            {
                                lowestProbability = probability;
                                bestCardIndex = i;
                                bubeSuit = (Suit)suit;
                            }
                        }
                    }
                    else
                    {
                        var probability = GetProbabilityForCard(player, card, nextPlayer);

                        if (card.face == Face._7)
                        {
                            if (probability < lowest7probability)
                            {
                                lowest7probability = probability;
                                best7Index = i;
                            }
                        }

                        if (probability < lowestProbability)
                        {
                            lowestProbability = probability;
                            bestCardIndex = i;
                        }
                    }
                }
            }

            if (bestCardIndex == -1)
            {
                // Draw Card.
                gameState.DrawCard(player);
                gameState.EndTurn();
            }
            else
            {
                if (gameState.playedCards.Last().face == Face._7 && best7Index != -1)
                    gameState.PlayCard(best7Index, bubeSuit.ToString());
                else
                    gameState.PlayCard(bestCardIndex, bubeSuit.ToString());
            }
        }

        private static double GetProbabilityForCard(Player player, Card card, Player nextPlayer)
        {
            double probability = 0;

            foreach (var state in player.otherPlayerInformation[nextPlayer].cardsFromCardState)
            {
                if (state.Value == 0) // || state.Key.cardCount == 0, but that'll be 0 in the first case.
                    continue;

                ulong mask = CardState.GetFaceMask(card.face) | CardState.GetSuitMask(card.suit) | CardState.GetFaceMask(Face.Bube);
                int possibleMatchingCards = state.Key.GetMatchingCardCount(mask);

                if (possibleMatchingCards == 0)
                    continue;

                probability = 1.0 - ((1.0 - probability) * Math.Pow(1 - (double)possibleMatchingCards / state.Key.cardCount, state.Value));
            }

            return probability;
        }

        private static void AddCardProbabilities(List<IEnumerable<HElement>> table, Player player, Card card, Player nextPlayer) => table.Add(new List<HElement>() { $"{card.suit} {card.face}", $"{GetProbabilityForCard(player, card, nextPlayer) * 100} %" });
    }
}
