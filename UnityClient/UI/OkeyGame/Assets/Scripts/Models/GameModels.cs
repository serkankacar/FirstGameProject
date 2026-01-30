using System;
using System.Collections.Generic;

namespace OkeyGame.Models
{
    /// <summary>
    /// Okey taşı modeli
    /// </summary>
    [Serializable]
    public class OkeyTile
    {
        public int Id;
        public TileColor Color;
        public int Number; // 1-13
        public bool IsFalseOkey; // Sahte okey (joker)
        public bool IsFaceDown; // Kapalı mı
        
        public bool IsOkey { get; set; } // Gerçek okey

        public OkeyTile() { }

        public OkeyTile(int id, TileColor color, int number, bool isFalseOkey = false)
        {
            Id = id;
            Color = color;
            Number = number;
            IsFalseOkey = isFalseOkey;
            IsFaceDown = false;
        }

        public string GetDisplayName()
        {
            if (IsFalseOkey) return "Joker";
            return $"{Color} {Number}";
        }

        public override string ToString()
        {
            return $"[{Id}] {Color}-{Number}" + (IsFalseOkey ? " (Joker)" : "") + (IsOkey ? " (OKEY)" : "");
        }

        public override bool Equals(object obj)
        {
            if (obj is OkeyTile other)
                return Id == other.Id;
            return false;
        }

        public override int GetHashCode() => Id.GetHashCode();
    }

    public enum TileColor
    {
        Yellow = 0,  // Sarı
        Blue = 1,    // Mavi
        Black = 2,   // Siyah
        Red = 3      // Kırmızı
    }

    /// <summary>
    /// Oyuncu modeli
    /// </summary>
    [Serializable]
    public class PlayerInfo
    {
        public string Id;
        public string PlayerId; // Backend alias
        public string Username;
        public string Name; // Display name alias
        public string PlayerName; // Another alias
        public string DisplayName;
        public long Chips;
        public int Elo;
        public int EloScore;
        public string AvatarUrl;
        public bool IsOnline;
        public bool IsReady;
        public bool IsHost;
        public bool IsBot;
        
        // Game specific
        public int SeatIndex;
        public int TileCount;
        public bool IsCurrentTurn;
        public bool HasDrawn; // Bu turda taş çekti mi
    }

    /// <summary>
    /// Oda modeli
    /// </summary>
    [Serializable]
    public class RoomInfo
    {
        public string Id;
        public string RoomId;
        public string Name;
        public string RoomName;
        public long Stake; // Masa bahsi
        public long TableStake; // Alias
        public int MinElo;
        public int MaxElo;
        public int CurrentPlayerCount;
        public int PlayerCount;
        public int MaxPlayers;
        public bool IsGameStarted;
        public RoomStatus Status;
        public List<PlayerInfo> Players;
        public DateTime CreatedAt;
    }

    public enum RoomStatus
    {
        WaitingForPlayers,
        Starting,
        InProgress,
        Finished
    }

    /// <summary>
    /// Oyun durumu - Backend'den gelen snapshot
    /// </summary>
    [Serializable]
    public class GameState
    {
        public string RoomId;
        public string CurrentTurnPlayerId;
        public int CurrentTurnSeatIndex;
        public int TurnNumber;
        public float TurnTimeRemaining;
        public GamePhase Phase;
        
        // Gösterge taşı (okey belirleyici)
        public OkeyTile IndicatorTile;
        
        // Orta alan
        public int DeckRemainingCount;
        public OkeyTile LastDiscardedTile;
        public List<OkeyTile> DiscardPile;
        
        // Oyuncu elleri (sadece kendi elimiz tam görünür)
        public List<OkeyTile> MyHand;
        public Dictionary<int, int> OpponentTileCounts; // SeatIndex -> TileCount
        
        // Oyuncu listesi
        public List<PlayerInfo> Players;
        
        // Açılan perler (kazanma durumu)
        public Dictionary<int, List<List<OkeyTile>>> RevealedSets; // SeatIndex -> Sets
    }

    public enum GamePhase
    {
        WaitingForPlayers,
        DealingTiles,
        Playing,
        TurnStart,
        WaitingForDraw,
        WaitingForDiscard,
        GameOver,
        Cancelled
    }

    /// <summary>
    /// Sıralama tablosu girişi
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public int Rank;
        public string PlayerId;
        public string Username;
        public string DisplayName;
        public int EloScore;
        public int TotalGamesPlayed;
        public int TotalGamesWon;
        public float WinRate;
    }

    /// <summary>
    /// Oyun sonucu
    /// </summary>
    [Serializable]
    public class GameEndResult
    {
        public string WinnerId;
        public string WinnerName;
        public string WinType; // Normal, Pairs, OkeyDiscard, DeckEmpty
        public int WinScore;
        public bool IsMyWin;
        public List<PlayerGameResult> PlayerResults;
    }

    [Serializable]
    public class PlayerGameResult
    {
        public string PlayerId;
        public string Username;
        public int Score;
        public long ChipChange;
        public int EloChange;
        public long NewChipBalance;
        public int NewEloScore;
    }

    /// <summary>
    /// API yanıt modelleri
    /// </summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public bool Success;
        public string Message;
        public T Data;
        public string Error;
    }

    [Serializable]
    public class LoginResponse
    {
        public string Token;
        public string PlayerId;
        public string Username;
        public long Chips;
        public int EloScore;
        public DateTime TokenExpiry;
    }

    [Serializable]
    public class RoomListResponse
    {
        public List<RoomInfo> Rooms;
        public int TotalCount;
    }

    /// <summary>
    /// Oyun başladığında gelen data
    /// </summary>
    [Serializable]
    public class GameStartedData
    {
        public string RoomId;
        public string Message;
        public OkeyTile IndicatorTile;
        public int FirstTurnSeatIndex;
        public List<OkeyTile> InitialHand;
        public int DeckCount;
        public List<PlayerInfo> Players;
        
        // Provably Fair
        public string ServerSeed;
        public string ServerSeedHash;
        public string ClientSeed;
    }

    /// <summary>
    /// Taş çekildiğinde gelen data (sadece çeken oyuncuya)
    /// </summary>
    [Serializable]
    public class TileDrawnData
    {
        public OkeyTile Tile;
        public bool FromDiscard;
        public string Timestamp;
    }

    /// <summary>
    /// Rakip taş çektiğinde gelen data
    /// </summary>
    [Serializable]
    public class OpponentDrewTileData
    {
        public string PlayerId;
        public bool FromDiscard;
        public string Timestamp;
    }

    /// <summary>
    /// Taş atıldığında gelen data
    /// </summary>
    [Serializable]
    public class TileDiscardedData
    {
        public string PlayerId;
        public int TileId;
        public TileData Tile;  // Tam taş bilgisi
        public string NextTurnPlayerId;
        public int NextTurnPosition;
        public string Timestamp;
    }

    /// <summary>
    /// Backend'den gelen taş bilgisi (OnTileDiscarded için)
    /// </summary>
    [Serializable]
    public class TileData
    {
        public int Id;
        public string Color;
        public int Number;
        public bool IsFalseJoker;
    }

    /// <summary>
    /// Deste güncelleme datası
    /// </summary>
    [Serializable]
    public class DeckUpdatedData
    {
        public int RemainingTileCount;
        public int DiscardPileCount;
    }
}
