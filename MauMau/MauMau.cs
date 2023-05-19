using LamestWebserver;
using LamestWebserver.UI;
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

    internal class MauMau : PageResponse
    {
        int playerTurnIndex = 0;
        List<string> lobby = new List<string>();
        List<string> players = new List<string>();
        Dictionary<string, List<Card>> cardsPerPlayer = new Dictionary<string, List<Card>>();
        List<Card> playedCards = new List<Card>();
        List<Card> availableCards = new List<Card>();
        bool gameStarted = false;
        bool isFirstTurn = true;
        bool lastTurnWasDraw = false;
        int sevenDrawCounter = 0;

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

            if (!gameStarted)
            {
                if (null != sessionData.HttpHeadVariables["start"])
                {
                    StartGame();
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

            if (!players.Contains(sessionData.UserName))
            {
                yield return new HContainer { Class = $"active_card{(playedCards.LastOrDefault().face == Face._7 && sevenDrawCounter > 0 ? $" draw _{sevenDrawCounter}" : "")}", Elements = { playedCards.LastOrDefault().ToButton(null) } };

                yield return new HList(HList.EListType.OrderedList, from p in players.OrderBy(x => cardsPerPlayer[x].Count) select new HContainer { Elements = { new HText($"{p}: {cardsPerPlayer[p].Count} Cards"), new HContainer { Class = "player_preview", Elements = (from c in cardsPerPlayer[p] select c.ToButton(null)).ToList() } } });

                yield return new HScript(ScriptCollection.GetPageReloadInMilliseconds, 1000);
                yield break;
            }

            yield return new HContainer { Class = $"active_card{(playedCards.LastOrDefault().face == Face._7 && sevenDrawCounter > 0 ? $" draw _{sevenDrawCounter}" : "")}", Elements = { playedCards.LastOrDefault().ToButton(null) } };

            yield return new HList(HList.EListType.UnorderedList, from p in players where p != sessionData.UserName select (HElement)new HText($"{p}: {cardsPerPlayer[p].Count}")) { Class = "other_players" };

            yield return new HText($"Available cards on draw pile: {availableCards.Count}") { Class = "available_cards" };

            yield return new HHeadline("Your Cards:", 2);

            if (players[playerTurnIndex] != sessionData.UserName) // if it's currently someone elses turn.
            {
                yield return new HContainer { Class = "your_cards", Elements = (from x in cardsPerPlayer[sessionData.UserName] select x.ToButton(null)).ToList() };
                yield return new HScript(ScriptCollection.GetPageReferalToXInMilliseconds, nameof(MauMau), 1000);
            }
            else // if it's currently our turn.
            {
                int initialPlayerTurnIndex = playerTurnIndex;
                var cards = cardsPerPlayer[sessionData.UserName];
                string playString = sessionData.HttpHeadVariables["play"];

                if (int.TryParse(playString, out int playIndex) && IsCardValidTurn(cards[playIndex]))
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
                        if (isFirstTurn && playedCards.LastOrDefault().face == Face._7 && card.face != Face._7 && sevenDrawCounter > 0)
                        {
                            for (int i = 0; i < sevenDrawCounter; i++)
                                cards.Add(DrawCard());

                            sevenDrawCounter = 0;
                            lastTurnWasDraw = true;
                        }

                        isFirstTurn = false;
                        lastTurnWasDraw = false;
                        playedCards.Add(card);
                        cards.RemoveAt(playIndex);

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

                            players.RemoveAt(initialPlayerTurnIndex);

                            if (players.Count <= 1)
                                gameStarted = false;
                        }

                        yield return new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau));
                        yield break;
                    }
                }

                if (null != sessionData.HttpHeadVariables["draw"] && !lastTurnWasDraw)
                {
                    if (isFirstTurn && playedCards.LastOrDefault().face == Face._7 && sevenDrawCounter > 0)
                    {
                        for (int i = 0; i < sevenDrawCounter; i++)
                            cards.Add(DrawCard());

                        sevenDrawCounter = 0;
                        isFirstTurn = false;
                        lastTurnWasDraw = true;
                    }
                    else
                    {
                        cards.Add(DrawCard());
                        EndTurn();

                        yield return new HScript(ScriptCollection.GetPageReferalToX, nameof(MauMau));
                        yield break;
                    }
                }
                else if (null != sessionData.HttpHeadVariables["end"])
                {
                    EndTurn();
                }

                bool anyValidCard = false;

                for (int i = 0; i < cards.Count; i++)
                {
                    bool isCardValid = IsCardValidTurn(cards[i]);
                    anyValidCard |= isCardValid;

                    yield return cards[i].ToButton(isCardValid ? i : (int?)null);
                }

                if (lastTurnWasDraw)
                {
                    if (anyValidCard)
                    {
                        yield return new HButton("End Turn", "?end") { Class = "action" };
                    }
                    else
                    {
                        EndTurn();

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

        private void EndTurn()
        {
            playerTurnIndex = (playerTurnIndex + 1) % players.Count;
            isFirstTurn = true;
            lastTurnWasDraw = false;
        }

        private bool IsCardValidTurn(Card card)
        {
            if (card.suit == playedCards.LastOrDefault().suit || card.face == playedCards.LastOrDefault().face || card.face == Face.Bube)
                return true;
            else
                return false;
        }

        private Card DrawCard()
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
            }
            
            var card = availableCards[0];
            availableCards.RemoveAt(0);

            return card;
        }

        private void Shuffle(List<Card> cards)
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

        private void StartGame()
        {
            playedCards = new List<Card>();
            availableCards = new List<Card>();
            cardsPerPlayer = new Dictionary<string, List<Card>>();
            playerTurnIndex = 0;
            sevenDrawCounter = 0;
            isFirstTurn = true;
            players = lobby.ToList();

            foreach (var suit in Enum.GetValues(typeof(Suit)))
                foreach (var face in Enum.GetValues(typeof(Face)))
                    availableCards.Add(new Card { suit = (Suit)suit, face = (Face)face });

            Shuffle(availableCards);

            // First card:
            playedCards.Add(DrawCard());

            foreach (var p in players)
                cardsPerPlayer.Add(p, new List<Card> { DrawCard(), DrawCard(), DrawCard(), DrawCard(), DrawCard() });

            gameStarted = true;
        }
    }
}
