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
        public string Username;
        public string DisplayName;
        public long Chips;
        public int EloScore;
        public string AvatarUrl;
        public bool IsOnline;
        public bool IsReady;
        
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
}
