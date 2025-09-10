using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameServices
{
    public static IDeckService Deck { get; private set; }


    public static void RegisterDeck(IDeckService deck)
    {
        Deck = deck;
    }
}
