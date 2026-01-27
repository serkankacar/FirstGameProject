using OkeyGame.Domain.Entities;
using Xunit;

namespace OkeyGame.Tests.Persistence;

/// <summary>
/// User entity testleri.
/// </summary>
public class UserTests
{
    #region Constructor Testleri

    [Fact]
    public void Constructor_ValidParameters_CreatesUser()
    {
        // Act
        var user = new User("testuser", "Test User");

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal(User.DefaultChips, user.Chips);
        Assert.Equal(User.DefaultEloScore, user.EloScore);
        Assert.True(user.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Constructor_InvalidUsername_ThrowsArgumentException(string? username)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new User(username!, "Display Name"));
    }

    [Fact]
    public void Constructor_UsernameTooShort_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new User("ab", "Display Name"));
    }

    [Fact]
    public void Constructor_UsernameTooLong_ThrowsArgumentException()
    {
        var longUsername = new string('a', User.MaxUsernameLength + 1);
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new User(longUsername, "Display Name"));
    }

    [Fact]
    public void Constructor_UsernameWithInvalidChars_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new User("test@user", "Display Name"));
    }

    [Fact]
    public void Constructor_NormalizesUsername_ToLowerCase()
    {
        // Act
        var user = new User("TestUser", "Test User");

        // Assert
        Assert.Equal("testuser", user.Username);
    }

    #endregion

    #region Chip İşlemleri Testleri

    [Fact]
    public void AddChips_PositiveAmount_IncreasesChips()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        long initialChips = user.Chips;

        // Act
        long newBalance = user.AddChips(1000);

        // Assert
        Assert.Equal(initialChips + 1000, newBalance);
        Assert.Equal(1000, user.TotalChipsWon);
    }

    [Fact]
    public void AddChips_ZeroAmount_ThrowsArgumentException()
    {
        var user = new User("testuser", "Test User");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => user.AddChips(0));
    }

    [Fact]
    public void AddChips_NegativeAmount_ThrowsArgumentException()
    {
        var user = new User("testuser", "Test User");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => user.AddChips(-100));
    }

    [Fact]
    public void DeductChips_SufficientBalance_DecreasesChips()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        long initialChips = user.Chips;

        // Act
        long newBalance = user.DeductChips(1000);

        // Assert
        Assert.Equal(initialChips - 1000, newBalance);
        Assert.Equal(1000, user.TotalChipsLost);
    }

    [Fact]
    public void DeductChips_InsufficientBalance_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        long excessAmount = user.Chips + 1;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => user.DeductChips(excessAmount));
    }

    [Fact]
    public void HasSufficientChips_EnoughBalance_ReturnsTrue()
    {
        var user = new User("testuser", "Test User");

        // Act & Assert
        Assert.True(user.HasSufficientChips(1000));
    }

    [Fact]
    public void HasSufficientChips_NotEnoughBalance_ReturnsFalse()
    {
        var user = new User("testuser", "Test User");

        // Act & Assert
        Assert.False(user.HasSufficientChips(user.Chips + 1));
    }

    #endregion

    #region ELO İşlemleri Testleri

    [Fact]
    public void UpdateEloScore_ValidScore_UpdatesScore()
    {
        // Arrange
        var user = new User("testuser", "Test User");

        // Act
        user.UpdateEloScore(1500);

        // Assert
        Assert.Equal(1500, user.EloScore);
        Assert.Equal(1500, user.HighestEloScore);
    }

    [Fact]
    public void UpdateEloScore_BelowMinimum_ClampsToMinimum()
    {
        // Arrange
        var user = new User("testuser", "Test User");

        // Act
        user.UpdateEloScore(50);

        // Assert
        Assert.Equal(User.MinEloScore, user.EloScore);
    }

    [Fact]
    public void UpdateEloScore_DoesNotReduceHighest()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        user.UpdateEloScore(1600);

        // Act
        user.UpdateEloScore(1400);

        // Assert
        Assert.Equal(1400, user.EloScore);
        Assert.Equal(1600, user.HighestEloScore);
    }

    [Fact]
    public void ApplyEloChange_PositiveChange_IncreasesScore()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        int initialElo = user.EloScore;

        // Act
        user.ApplyEloChange(50);

        // Assert
        Assert.Equal(initialElo + 50, user.EloScore);
    }

    [Fact]
    public void ApplyEloChange_NegativeChange_DecreasesScore()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        int initialElo = user.EloScore;

        // Act
        user.ApplyEloChange(-30);

        // Assert
        Assert.Equal(initialElo - 30, user.EloScore);
    }

    #endregion

    #region Oyun İstatistikleri Testleri

    [Fact]
    public void RecordGameResult_Win_IncreasesWinCount()
    {
        // Arrange
        var user = new User("testuser", "Test User");

        // Act
        user.RecordGameResult(isWin: true);

        // Assert
        Assert.Equal(1, user.TotalGamesPlayed);
        Assert.Equal(1, user.TotalGamesWon);
        Assert.Equal(100.0, user.WinRate);
    }

    [Fact]
    public void RecordGameResult_Loss_DoesNotIncreaseWinCount()
    {
        // Arrange
        var user = new User("testuser", "Test User");

        // Act
        user.RecordGameResult(isWin: false);

        // Assert
        Assert.Equal(1, user.TotalGamesPlayed);
        Assert.Equal(0, user.TotalGamesWon);
        Assert.Equal(0.0, user.WinRate);
    }

    [Fact]
    public void WinRate_CalculatesCorrectly()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        user.RecordGameResult(isWin: true);
        user.RecordGameResult(isWin: true);
        user.RecordGameResult(isWin: false);
        user.RecordGameResult(isWin: false);

        // Assert
        Assert.Equal(4, user.TotalGamesPlayed);
        Assert.Equal(2, user.TotalGamesWon);
        Assert.Equal(50.0, user.WinRate);
    }

    #endregion

    #region Profil Güncelleme Testleri

    [Fact]
    public void UpdateDisplayName_ValidName_UpdatesName()
    {
        // Arrange
        var user = new User("testuser", "Test User");

        // Act
        user.UpdateDisplayName("New Name");

        // Assert
        Assert.Equal("New Name", user.DisplayName);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        // Arrange
        var user = new User("testuser", "Test User");

        // Act
        user.Deactivate();

        // Assert
        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        // Arrange
        var user = new User("testuser", "Test User");
        user.Deactivate();

        // Act
        user.Activate();

        // Assert
        Assert.True(user.IsActive);
    }

    #endregion
}
