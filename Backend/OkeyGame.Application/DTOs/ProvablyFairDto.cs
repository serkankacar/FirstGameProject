namespace OkeyGame.Application.DTOs;

/// <summary>
/// Oyun başında istemciye gönderilen Commitment DTO.
/// Sadece hash gönderilir, seed ve state GİZLİ tutulur.
/// </summary>
public class CommitmentDto
{
    /// <summary>
    /// HMAC-SHA256 ile hesaplanmış commitment hash.
    /// Oyuncular bu hash'i saklayarak oyun sonunda doğrulama yapabilir.
    /// </summary>
    public required string CommitmentHash { get; init; }

    /// <summary>
    /// Oyun numarası/sayacı.
    /// </summary>
    public required long Nonce { get; init; }

    /// <summary>
    /// Commitment oluşturulma zamanı (UTC).
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// İstemci seed'i kabul ediliyor mu?
    /// True ise istemci kendi seed'ini gönderebilir.
    /// </summary>
    public bool AcceptsClientSeed { get; init; } = true;
}

/// <summary>
/// Oyun sonunda istemciye gönderilen Reveal DTO.
/// Tüm gizli veriler açıklanır.
/// </summary>
public class RevealDto
{
    /// <summary>
    /// Sunucu seed'i (Guid string olarak).
    /// </summary>
    public required string ServerSeed { get; init; }

    /// <summary>
    /// Taşların başlangıç dizilişi (JSON).
    /// </summary>
    public required string InitialState { get; init; }

    /// <summary>
    /// Oyun sayacı.
    /// </summary>
    public required long Nonce { get; init; }

    /// <summary>
    /// İstemci seed'i (varsa).
    /// </summary>
    public string? ClientSeed { get; init; }

    /// <summary>
    /// Commitment hash (doğrulama için).
    /// </summary>
    public required string CommitmentHash { get; init; }

    /// <summary>
    /// Commitment oluşturulma zamanı.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Açıklanma zamanı.
    /// </summary>
    public required DateTime RevealedAt { get; init; }
}

/// <summary>
/// Doğrulama isteği DTO.
/// İstemci bu verileri göndererek sunucudan doğrulama isteyebilir.
/// </summary>
public class VerifyRequestDto
{
    /// <summary>
    /// Sunucu seed'i.
    /// </summary>
    public required string ServerSeed { get; init; }

    /// <summary>
    /// Taşların başlangıç dizilişi.
    /// </summary>
    public required string InitialState { get; init; }

    /// <summary>
    /// Oyun sayacı.
    /// </summary>
    public required long Nonce { get; init; }

    /// <summary>
    /// İstemci seed'i (varsa).
    /// </summary>
    public string? ClientSeed { get; init; }

    /// <summary>
    /// Beklenen commitment hash.
    /// </summary>
    public required string ExpectedHash { get; init; }
}

/// <summary>
/// Doğrulama sonucu DTO.
/// </summary>
public class VerifyResultDto
{
    /// <summary>
    /// Doğrulama başarılı mı?
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Hesaplanan hash.
    /// </summary>
    public string? ComputedHash { get; init; }

    /// <summary>
    /// Beklenen hash.
    /// </summary>
    public required string ExpectedHash { get; init; }

    /// <summary>
    /// Doğrulama mesajı.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Doğrulama zamanı.
    /// </summary>
    public required DateTime VerifiedAt { get; init; }
}

/// <summary>
/// İstemci seed gönderme isteği.
/// </summary>
public class ClientSeedDto
{
    /// <summary>
    /// İstemci tarafından oluşturulan seed.
    /// Sunucu bu seed'i karıştırma algoritmasına dahil eder.
    /// </summary>
    public required string ClientSeed { get; init; }
}
