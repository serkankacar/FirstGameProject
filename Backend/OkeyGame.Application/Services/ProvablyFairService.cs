using System.Text.Json;
using OkeyGame.Application.DTOs;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Services;
using OkeyGame.Domain.ValueObjects;

namespace OkeyGame.Application.Services;

/// <summary>
/// Provably Fair sistemini yöneten Application Service.
/// 
/// KULLANIM AKIŞI:
/// 1. CreateCommitment() → Oyun başında commitment oluştur
/// 2. GetCommitmentDto() → Hash'i istemciye gönder
/// 3. SetClientSeed() → İstemci seed'ini al (opsiyonel)
/// 4. RevealCommitment() → Oyun sonunda verileri açıkla
/// 5. VerifyGame() → Doğrulama yap
/// </summary>
public class ProvablyFairService
{
    #region Alanlar

    /// <summary>
    /// Aktif oyunların commitment'ları.
    /// Key: RoomId, Value: Commitment
    /// </summary>
    private readonly Dictionary<Guid, ProvablyFairCommitment> _commitments;

    /// <summary>
    /// Oyun sayacı (her oyun için artar).
    /// </summary>
    private long _nonceCounter;

    /// <summary>
    /// Thread-safety için lock objesi.
    /// </summary>
    private readonly object _lock = new();

    #endregion

    #region Constructor

    public ProvablyFairService()
    {
        _commitments = new Dictionary<Guid, ProvablyFairCommitment>();
        _nonceCounter = 0;
    }

    #endregion

    #region Commitment Oluşturma

    /// <summary>
    /// Yeni bir commitment oluşturur ve saklar.
    /// Oyun başlamadan önce çağrılır.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    /// <param name="shuffledTiles">Karıştırılmış taş listesi</param>
    /// <returns>Oluşturulan commitment</returns>
    public ProvablyFairCommitment CreateCommitment(Guid roomId, List<Tile> shuffledTiles)
    {
        ArgumentNullException.ThrowIfNull(shuffledTiles);

        lock (_lock)
        {
            // Nonce'u artır
            _nonceCounter++;

            // Commitment oluştur
            var commitment = ProvablyFairCommitment.Create(
                shuffledTiles,
                _nonceCounter,
                tile => new
                {
                    tile.Id,
                    Color = tile.Color.ToString(),
                    tile.Value,
                    tile.IsFalseJoker
                });

            // Sakla
            _commitments[roomId] = commitment;

            return commitment;
        }
    }

    /// <summary>
    /// Mevcut commitment'ı döndürür.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    /// <returns>Commitment veya null</returns>
    public ProvablyFairCommitment? GetCommitment(Guid roomId)
    {
        lock (_lock)
        {
            return _commitments.TryGetValue(roomId, out var commitment) 
                ? commitment 
                : null;
        }
    }

    #endregion

    #region DTO Dönüşümleri

    /// <summary>
    /// İstemciye gönderilecek Commitment DTO'sunu oluşturur.
    /// Sadece hash gönderilir, gizli veriler GÖNDERILMEZ.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    /// <returns>Commitment DTO</returns>
    public CommitmentDto? GetCommitmentDto(Guid roomId)
    {
        var commitment = GetCommitment(roomId);
        if (commitment == null) return null;

        return new CommitmentDto
        {
            CommitmentHash = commitment.CommitmentHash,
            Nonce = commitment.Nonce,
            CreatedAt = commitment.CreatedAt,
            AcceptsClientSeed = !commitment.IsRevealed
        };
    }

    #endregion

    #region Client Seed

    /// <summary>
    /// İstemci seed'ini ayarlar.
    /// Bu, sunucunun tek başına kontrol edemeyeceği entropi ekler.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    /// <param name="clientSeed">İstemci seed'i</param>
    /// <returns>Güncellenmiş commitment</returns>
    public ProvablyFairCommitment? SetClientSeed(Guid roomId, string clientSeed)
    {
        if (string.IsNullOrWhiteSpace(clientSeed))
        {
            throw new ArgumentException("Client seed boş olamaz.", nameof(clientSeed));
        }

        lock (_lock)
        {
            if (!_commitments.TryGetValue(roomId, out var commitment))
            {
                return null;
            }

            // Client seed ile yeni commitment oluştur
            var updatedCommitment = commitment.WithClientSeed(clientSeed);
            _commitments[roomId] = updatedCommitment;

            return updatedCommitment;
        }
    }

    #endregion

    #region Reveal (Açıklama)

    /// <summary>
    /// Commitment'ı açıklar ve reveal verilerini döndürür.
    /// Oyun sonunda çağrılır.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    /// <returns>Reveal DTO veya null</returns>
    public RevealDto? RevealCommitment(Guid roomId)
    {
        lock (_lock)
        {
            if (!_commitments.TryGetValue(roomId, out var commitment))
            {
                return null;
            }

            // Açıkla
            commitment.Reveal();

            // DTO oluştur
            var revealData = commitment.GetRevealData();
            return new RevealDto
            {
                ServerSeed = revealData.ServerSeed,
                InitialState = revealData.InitialState,
                Nonce = revealData.Nonce,
                ClientSeed = revealData.ClientSeed,
                CommitmentHash = revealData.CommitmentHash,
                CreatedAt = revealData.CreatedAt,
                RevealedAt = revealData.RevealedAt
            };
        }
    }

    #endregion

    #region Doğrulama

    /// <summary>
    /// Oyunun adil olup olmadığını doğrular.
    /// </summary>
    /// <param name="request">Doğrulama isteği</param>
    /// <returns>Doğrulama sonucu</returns>
    public VerifyResultDto VerifyGame(VerifyRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = ProvablyFairVerifier.Verify(
            request.ServerSeed,
            request.InitialState,
            request.Nonce,
            request.ExpectedHash,
            request.ClientSeed);

        return new VerifyResultDto
        {
            IsValid = result.IsValid,
            ComputedHash = result.ComputedHash,
            ExpectedHash = result.ExpectedHash,
            Message = result.Message,
            VerifiedAt = result.VerifiedAt
        };
    }

    /// <summary>
    /// Reveal verilerini kullanarak doğrulama yapar.
    /// </summary>
    /// <param name="revealDto">Reveal verileri</param>
    /// <returns>Doğrulama sonucu</returns>
    public VerifyResultDto VerifyFromReveal(RevealDto revealDto)
    {
        ArgumentNullException.ThrowIfNull(revealDto);

        return VerifyGame(new VerifyRequestDto
        {
            ServerSeed = revealDto.ServerSeed,
            InitialState = revealDto.InitialState,
            Nonce = revealDto.Nonce,
            ClientSeed = revealDto.ClientSeed,
            ExpectedHash = revealDto.CommitmentHash
        });
    }

    #endregion

    #region Temizlik

    /// <summary>
    /// Tamamlanmış oyunun commitment'ını temizler.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    public void CleanupCommitment(Guid roomId)
    {
        lock (_lock)
        {
            _commitments.Remove(roomId);
        }
    }

    #endregion
}
