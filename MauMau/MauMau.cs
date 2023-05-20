using LamestWebserver;
using LamestWebserver.UI;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
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
        internal Suit suit;
        internal Face face;

        private HElement ToButtonInternal(int? index, string className, string additionalParams = "")
        {
            return new HButton("", index.HasValue ? $"?play={index.Value}{additionalParams}" : "")
            {
                Class = $"{className}{(index.HasValue ? " playable" : "")}",
                Elements =
                    {
                        new HContainer { Class = $"suit {suit}" },
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
                        new Card(){ face = Face.Bube, suit = Suit.Kreuz }.ToButtonInternal(index, "subcard", $"&suit={Suit.Kreuz}"),
                        new Card(){ face = Face.Bube, suit = Suit.Pik }.ToButtonInternal(index, "subcard", $"&suit={Suit.Pik}"),
                        new Card(){ face = Face.Bube, suit = Suit.Karo }.ToButtonInternal(index, "subcard", $"&suit={Suit.Karo}"),
                        new Card(){ face = Face.Bube, suit = Suit.Herz }.ToButtonInternal(index, "subcard", $"&suit={Suit.Herz}"),
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

        public void RemoveCard(Suit s, Face f)
        {
            cardsLeft &= ~((ulong)1 << (int)(((int)s * Enum.GetValues(typeof(Face)).Length) + (int)f));
            cardCount--;
        }

        public void AddCard(Suit s, Face f)
        {
            cardsLeft |= ((ulong)1 << (int)(((int)s * Enum.GetValues(typeof(Face)).Length) + (int)f));
            cardCount++;
        }

        public int GetSuitCards(Suit s)
        {
            ulong suitMask = (((ulong)1 << Enum.GetValues(typeof(Face)).Length) - 1) << ((int)s);

            return PopCnt(cardsLeft & suitMask);
        }

        public int GetFaceCards(Face f)
        {
            ulong faceMask = (ulong)(1 << (int)f) * (ulong)(0x99); // relies on number of faces being 8.

            return PopCnt(cardsLeft & faceMask);
        }

        // https://stackoverflow.com/a/11517887 ????
        private static byte PopCnt(ulong value)
        {
            ulong result = value - ((value >> 1) & 0x5555555555555555UL);
            result = (result & 0x3333333333333333UL) + ((result >> 2) & 0x3333333333333333UL);
            return (byte)(unchecked(((result + (result >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }
    }

    internal class Player
    {
        public List<Card> cards = new List<Card>();
        public List<CardState> cardStates = new List<CardState>();

        public void AddCardState(CardState cardState)
        {
            cardStates.Add(cardState);
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

        public bool IsCardValidTurn(Card card)
        {
            if (card.suit == playedCards.LastOrDefault().suit || card.face == playedCards.LastOrDefault().face || card.face == Face.Bube)
                return true;
            else
                return false;
        }

        public void DrawCard(Player p)
        {
            if (availableCards.Count < 1)
            {
                Card lastPlayedCard = playedCards.LastOrDefault();
                playedCards.RemoveAt(playedCards.Count - 1);

                var tmp = availableCards;
                availableCards = playedCards;
                playedCards = tmp;

                Shuffle(availableCards);

                playedCards.Add(lastPlayedCard);

                foreach (var x in players.Values)
                    x.AddCardState(new CardState(availableCards));
            }


            var card = availableCards[0];
            availableCards.RemoveAt(0);

            if (p == null) // first ever Card gets drawn.
            {
                playedCards.Add(card);
            }
            else
            {
                p.cards.Add(card);
                p.cardStates.Last().RemoveCard(card.suit, card.face);
            }
        }

        public void Shuffle(List<Card> cards)
        {
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

            foreach (var l in lobby)
                players.Add(l, new Player());

            foreach (var suit in Enum.GetValues(typeof(Suit)))
                foreach (var face in Enum.GetValues(typeof(Face)))
                    availableCards.Add(new Card { suit = (Suit)suit, face = (Face)face });

            Shuffle(availableCards);

            // First card:
            DrawCard(null);

            foreach (var p in players.Values)
            {
                for (int i = 0; i < 5; i++)
                    DrawCard(p);

                p.AddCardState(new CardState(p, playedCards.Last()));
            }

            gameStarted = true;
        }
    }

    internal class MauMau : PageResponse
    {
        List<string> lobby = new List<string>();
        GameState gameState = new GameState();

        public MauMau() : base(nameof(MauMau)) { }

        protected override string GetContents(SessionData sessionData)
        {
            // Get the default layout around the elements retrieved by GetElements()
            HElement page = MainPage.GetPage(GetElements(sessionData as HttpSessionData), "MauMau");

            // To get the HTML-string of an HElement, call GetContent with the current session data.
            return page.GetContent(sessionData);
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

                yield return new HHeadline("Lobby:", 2);
                yield return new HList(HList.EListType.UnorderedList, from x in lobby select new HText(x));

                if (lobby.Count > 1)
                    yield return new HLink("Start Game.", $"{nameof(MauMau)}?start");

                yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 1000);
                yield break;
            }

            if (!gameState.players.ContainsKey(sessionData.UserName))
            {
                yield return new HContainer { Class = $"active_card{(gameState.playedCards.LastOrDefault().face == Face._7 && gameState.sevenDrawCounter > 0 ? $" draw _{gameState.sevenDrawCounter}" : "")}", Elements = { gameState.playedCards.LastOrDefault().ToButton(null) } };

                yield return new HList(HList.EListType.OrderedList, from p in gameState.players.OrderBy(x => gameState.players[x.Key].cards.Count) select new HContainer { Elements = { new HText($"{p}: {gameState.players[p.Key].cards.Count} Cards"), new HContainer { Class = "player_preview", Elements = (from c in gameState.players[p.Key].cards select c.ToButton(null)).ToList() } } });

                yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 1000);
                yield break;
            }

            yield return new HContainer { Class = $"active_card{(gameState.playedCards.LastOrDefault().face == Face._7 && gameState.sevenDrawCounter > 0 ? $" draw _{gameState.sevenDrawCounter}" : "")}", Elements = { gameState.playedCards.LastOrDefault().ToButton(null) } };

            yield return new HList(HList.EListType.UnorderedList, from p in gameState.players where p.Key != sessionData.UserName select (HElement)new HText($"{p.Key}: {gameState.players[p.Key].cards.Count}")) { Class = "other_players" };

            yield return new HText($"Draw Pile: {gameState.availableCards.Count}") { Class = "available_cards" };

            yield return new HHeadline("Your Cards:", 2);

            // not sure if ToList returns the Keys in the correct order for every functioncall.
            if (gameState.players.Keys.ToList()[gameState.playerTurnIndex] != sessionData.UserName) // if it's currently someone elses turn.
            {
                yield return new HContainer { Class = "your_cards", Elements = (from x in gameState.players[sessionData.UserName].cards select x.ToButton(null)).ToList() };
                yield return new HScript(ScriptCollection.GetPageReferalToXInMilliseconds, nameof(MauMau), 1000);
            }
            else // if it's currently our turn.
            {
                int initialPlayerTurnIndex = gameState.playerTurnIndex;
                var cards = gameState.players[sessionData.UserName].cards;
                string playString = sessionData.HttpHeadVariables["play"];

                if (int.TryParse(playString, out int playIndex) && gameState.IsCardValidTurn(cards[playIndex]))
                {
                    Card card = cards[playIndex];
                    bool validCard = true;

                    switch (card.face)
                    {
                        case Face.Bube:
                        {
                            if (Enum.TryParse(sessionData.HttpHeadVariables["suit"], out Suit chosenSuit))
                                card.suit = chosenSuit;
                            else
                                validCard = false;

                            break;
                        }
                    }

                    if (validCard)
                    {
                        if (gameState.isFirstTurn && gameState.playedCards.LastOrDefault().face == Face._7 && card.face != Face._7 && gameState.sevenDrawCounter > 0)
                        {
                            for (int i = 0; i < gameState.sevenDrawCounter; i++)
                                gameState.DrawCard(gameState.players[sessionData.UserName]);

                            gameState.sevenDrawCounter = 0;
                            gameState.lastTurnWasDraw = true;
                        }

                        gameState.isFirstTurn = false;
                        gameState.lastTurnWasDraw = false;
                        gameState.playedCards.Add(card);
                        cards.RemoveAt(playIndex);

                        foreach (var p in (from x in gameState.players where x.Key != sessionData.UserName select x))
                            p.Value.cardStates.Last().RemoveCard(gameState.playedCards.Last().suit, gameState.playedCards.Last().face);

                        switch (card.face)
                        {
                            case Face.Ass:
                                break;

                            case Face._8:
                                gameState.playerTurnIndex++;
                                gameState.EndTurn();
                                break;

                            case Face._7:
                                gameState.sevenDrawCounter += 2;
                                gameState.EndTurn();
                                break;

                            default:
                                gameState.EndTurn();
                                break;
                        }

                        if (cards.Count == 0 && gameState.playerTurnIndex != initialPlayerTurnIndex) // you're out.
                        {
                            if (gameState.playerTurnIndex > initialPlayerTurnIndex)
                                gameState.playerTurnIndex--;

                            gameState.players.Remove(gameState.players.Keys.ToList()[initialPlayerTurnIndex]); // this is bad. still relying on keys.ToList() to always be in the same order, but now we may even know that it is in fact the same order, since it's ... ordered. thanks avl-tree!

                            if (gameState.players.Count <= 1)
                                gameState.gameStarted = false;
                        }

                        yield return new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau));
                        yield break;
                    }
                }

                if (null != sessionData.HttpHeadVariables["draw"] && !gameState.lastTurnWasDraw)
                {
                    if (gameState.isFirstTurn && gameState.playedCards.LastOrDefault().face == Face._7 && gameState.sevenDrawCounter > 0)
                    {
                        for (int i = 0; i < gameState.sevenDrawCounter; i++)
                            gameState.DrawCard(gameState.players[sessionData.UserName]);

                        gameState.sevenDrawCounter = 0;
                        gameState.isFirstTurn = false;
                        gameState.lastTurnWasDraw = true;
                    }
                    else
                    {
                        gameState.DrawCard(gameState.players[sessionData.UserName]);
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
            }
        }
    }
}
