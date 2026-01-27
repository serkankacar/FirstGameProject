namespace OkeyGame.Domain.Enums;

/// <summary>
/// Oyunun detaylı evrelerini temsil eder.
/// State Machine için kullanılır.
/// 
/// OYUN AKIŞI:
/// WaitingForPlayers -> ReadyToStart -> Shuffling -> Dealing -> Playing -> Finished
///                                                                  ↓
///                                                             Cancelled
/// 
/// Playing evresinde alt durumlar:
/// - WaitingForDraw: Oyuncu taş çekmeli
/// - WaitingForDiscard: Oyuncu taş atmalı
/// </summary>
public enum GamePhase
{
    /// <summary>
    /// Oyuncular bekleniyor.
    /// Henüz 4 oyuncu tamamlanmadı.
    /// </summary>
    WaitingForPlayers = 0,

    /// <summary>
    /// Oyun başlamaya hazır.
    /// 4 oyuncu tamamlandı, host başlatma bekleniyor.
    /// </summary>
    ReadyToStart = 1,

    /// <summary>
    /// Taşlar karıştırılıyor.
    /// Provably Fair seed'leri oluşturuluyor.
    /// </summary>
    Shuffling = 2,

    /// <summary>
    /// Taşlar dağıtılıyor.
    /// Her oyuncuya taşlar veriliyor.
    /// </summary>
    Dealing = 3,

    /// <summary>
    /// Oyun devam ediyor.
    /// Oyuncular sırayla oynuyor.
    /// </summary>
    Playing = 4,

    /// <summary>
    /// Oyun bitti.
    /// Bir oyuncu kazandı veya deste bitti.
    /// </summary>
    Finished = 5,

    /// <summary>
    /// Oyun iptal edildi.
    /// Oyuncu ayrıldı veya hata oluştu.
    /// </summary>
    Cancelled = 6
}

/// <summary>
/// Playing fazındaki alt durumları temsil eder.
/// </summary>
public enum TurnPhase
{
    /// <summary>
    /// Oyuncu taş çekmeyi bekliyor.
    /// Desteden veya discard'dan çekebilir.
    /// </summary>
    WaitingForDraw = 0,

    /// <summary>
    /// Oyuncu taş atmayı bekliyor.
    /// Elinden bir taş atmalı.
    /// </summary>
    WaitingForDiscard = 1,

    /// <summary>
    /// Oyuncu sırasını tamamladı.
    /// Sıra sonraki oyuncuya geçiyor.
    /// </summary>
    TurnCompleted = 2
}
