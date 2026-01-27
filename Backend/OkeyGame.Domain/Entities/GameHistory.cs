using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Entities;

/// <summary>
/// Oyun geçmişi entity'si.
/// Oynanan oyunların detaylı loglarını tutar.
/// </summary>
public class GameHistory
{
    #region Primary Key

    /// <summary>
    /// Oyun geçmişi benzersiz kimliği.
    /// </summary>
    public Guid Id { get; private set; }

    #endregion

    #region Oyun Bilgileri

    /// <summary>
    /// Oda ID'si.
    /// </summary>
    public Guid RoomId { get; private set; }

    /// <summary>
    /// Oyunun başlangıç zamanı.
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// Oyunun bitiş zamanı.
    /// </summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>
    /// Oyun süresi (saniye).
    /// </summary>
    public int DurationSeconds => EndedAt.HasValue 
        ? (int)(EndedAt.Value - StartedAt).TotalSeconds 
        : 0;

    /// <summary>
    /// Oyunun son durumu.
    /// </summary>
    public GameHistoryStatus Status { get; private set; }

    /// <summary>
    /// Toplam tur sayısı.
    /// </summary>
    public int TotalTurns { get; private set; }

    #endregion

    #region Oyuncu Bilgileri

    /// <summary>
    /// Kazanan oyuncu ID'si.
    /// </summary>
    public Guid? WinnerId { get; private set; }

    /// <summary>
    /// Kazanan oyuncu kullanıcı adı (denormalized).
    /// </summary>
    public string? WinnerUsername { get; private set; }

    /// <summary>
    /// Kazanma tipi.
    /// </summary>
    public WinType? WinType { get; private set; }

    /// <summary>
    /// Kazanma puanı.
    /// </summary>
    public int? WinScore { get; private set; }

    #endregion

    #region Ekonomi

    /// <summary>
    /// Masa bahsi (her oyuncunun yatırdığı).
    /// </summary>
    public long TableStake { get; private set; }

    /// <summary>
    /// Toplam pot (4 oyuncu için).
    /// </summary>
    public long TotalPot => TableStake * 4;

    /// <summary>
    /// Platform komisyonu (rake).
    /// </summary>
    public long RakeAmount { get; private set; }

    /// <summary>
    /// Kazanana ödenen net miktar.
    /// </summary>
    public long WinnerPayout => TotalPot - RakeAmount;

    #endregion

    #region Oyuncu Detayları (JSON)

    /// <summary>
    /// Oyuncu sonuçları (JSON serialized).
    /// Her oyuncu için: ID, Username, Position, FinalChips, EloChange, ChipChange.
    /// </summary>
    public string PlayerResultsJson { get; private set; } = "[]";

    #endregion

    #region Provably Fair

    /// <summary>
    /// Oyun seed'i (hash için).
    /// </summary>
    public string? GameSeed { get; private set; }

    /// <summary>
    /// Server seed hash (doğrulama için).
    /// </summary>
    public string? ServerSeedHash { get; private set; }

    /// <summary>
    /// Client seed (oyuncudan gelen).
    /// </summary>
    public string? ClientSeed { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// EF Core için private constructor.
    /// </summary>
    private GameHistory() { }

    /// <summary>
    /// Yeni oyun geçmişi oluşturur.
    /// </summary>
    public static GameHistory Create(
        Guid roomId,
        long tableStake,
        string? serverSeedHash = null)
    {
        return new GameHistory
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            TableStake = tableStake,
            StartedAt = DateTime.UtcNow,
            Status = GameHistoryStatus.InProgress,
            TotalTurns = 0,
            RakeAmount = 0,
            ServerSeedHash = serverSeedHash
        };
    }

    #endregion

    #region Durum Güncellemeleri

    /// <summary>
    /// Tur sayısını artırır.
    /// </summary>
    public void IncrementTurn()
    {
        TotalTurns++;
    }

    /// <summary>
    /// Oyunu tamamlar.
    /// </summary>
    public void Complete(
        Guid winnerId,
        string winnerUsername,
        WinType winType,
        int winScore,
        long rakeAmount,
        string playerResultsJson)
    {
        if (Status != GameHistoryStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Oyun zaten tamamlanmış veya iptal edilmiş. Durum: {Status}");
        }

        WinnerId = winnerId;
        WinnerUsername = winnerUsername;
        WinType = winType;
        WinScore = winScore;
        RakeAmount = rakeAmount;
        PlayerResultsJson = playerResultsJson;
        EndedAt = DateTime.UtcNow;
        Status = GameHistoryStatus.Completed;
    }

    /// <summary>
    /// Oyunu iptal eder (bağlantı kopması vb.).
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status != GameHistoryStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Oyun zaten tamamlanmış veya iptal edilmiş. Durum: {Status}");
        }

        EndedAt = DateTime.UtcNow;
        Status = GameHistoryStatus.Cancelled;
        PlayerResultsJson = $"{{\"cancelReason\": \"{reason}\"}}";
    }

    /// <summary>
    /// Oyunu zaman aşımıyla bitirir.
    /// </summary>
    public void Timeout()
    {
        if (Status != GameHistoryStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Oyun zaten tamamlanmış veya iptal edilmiş. Durum: {Status}");
        }

        EndedAt = DateTime.UtcNow;
        Status = GameHistoryStatus.Timeout;
    }

    #endregion

    #region Provably Fair

    /// <summary>
    /// Client seed'i kaydeder.
    /// </summary>
    public void SetClientSeed(string clientSeed)
    {
        if (string.IsNullOrWhiteSpace(clientSeed))
        {
            throw new ArgumentException("Client seed boş olamaz.", nameof(clientSeed));
        }

        ClientSeed = clientSeed;
    }

    /// <summary>
    /// Oyun seed'ini açıklar (oyun bittikten sonra).
    /// </summary>
    public void RevealGameSeed(string gameSeed)
    {
        if (Status == GameHistoryStatus.InProgress)
        {
            throw new InvalidOperationException(
                "Oyun devam ederken seed açıklanamaz.");
        }

        GameSeed = gameSeed;
    }

    #endregion
}

/// <summary>
/// Oyun geçmişi durumu.
/// </summary>
public enum GameHistoryStatus
{
    /// <summary>Oyun devam ediyor.</summary>
    InProgress = 0,

    /// <summary>Oyun normal şekilde tamamlandı.</summary>
    Completed = 1,

    /// <summary>Oyun iptal edildi.</summary>
    Cancelled = 2,

    /// <summary>Oyun zaman aşımına uğradı.</summary>
    Timeout = 3
}
