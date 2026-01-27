using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;

namespace OkeyGame.Demo;

/// <summary>
/// Okey oyunu konsol demosu.
/// 4 bot birbiriyle oynar veya 1 insan + 3 bot.
/// </summary>
public class GameDemo
{
    #region Alanlar

    private readonly OkeyRuleEngine _ruleEngine;
    private readonly BotManager _botManager;
    private readonly List<DemoPlayer> _players;
    private readonly Queue<Tile> _deck;
    private readonly Stack<Tile> _discardPile;
    
    private Tile _indicatorTile = null!;
    private int _currentPlayerIndex;
    private bool _gameOver;
    private DemoPlayer? _winner;
    private int _turnCount;
    private const int MaxTurns = 200;

    #endregion

    #region Constructor

    public GameDemo(bool humanPlayer = false)
    {
        _ruleEngine = OkeyRuleEngine.Instance;
        _botManager = new BotManager();
        _players = new List<DemoPlayer>();
        _deck = new Queue<Tile>();
        _discardPile = new Stack<Tile>();

        InitializePlayers(humanPlayer);
    }

    #endregion

    #region Tile Set Oluşturma

    /// <summary>
    /// 106 taşlık seti oluşturur.
    /// 4 renk × 13 sayı × 2 kopya = 104 + 2 sahte okey = 106
    /// </summary>
    private List<Tile> CreateTileSet()
    {
        var tiles = new List<Tile>();
        int id = 1;

        // 4 renk × 13 sayı × 2 kopya
        for (int copy = 0; copy < 2; copy++)
        {
            foreach (TileColor color in Enum.GetValues<TileColor>())
            {
                for (int value = 1; value <= 13; value++)
                {
                    tiles.Add(Tile.Create(id++, color, value));
                }
            }
        }

        // 2 sahte okey
        tiles.Add(Tile.CreateFalseJoker(id++));
        tiles.Add(Tile.CreateFalseJoker(id++));

        return tiles;
    }

    /// <summary>
    /// Fisher-Yates shuffle algoritması.
    /// </summary>
    private void ShuffleTiles(List<Tile> tiles)
    {
        var random = new Random();
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
    }

    #endregion

    #region Initialization

    private void InitializePlayers(bool humanPlayer)
    {
        if (humanPlayer)
        {
            // İnsan oyuncu
            _players.Add(new DemoPlayer
            {
                Id = Guid.NewGuid(),
                Name = "👤 SEN",
                IsHuman = true,
                SeatIndex = 0
            });

            // 3 Bot
            var difficulties = new[] { BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Expert };
            for (int i = 0; i < 3; i++)
            {
                var bot = _botManager.CreateBot(difficulties[i]);
                _players.Add(new DemoPlayer
                {
                    Id = bot.PlayerId,
                    Name = $"🤖 Bot-{i + 1} ({difficulties[i]})",
                    IsHuman = false,
                    Bot = bot,
                    SeatIndex = i + 1
                });
            }
        }
        else
        {
            // 4 Bot
            var difficulties = new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Expert };
            for (int i = 0; i < 4; i++)
            {
                var bot = _botManager.CreateBot(difficulties[i]);
                _players.Add(new DemoPlayer
                {
                    Id = bot.PlayerId,
                    Name = $"🤖 Bot-{i + 1} ({difficulties[i]})",
                    IsHuman = false,
                    Bot = bot,
                    SeatIndex = i
                });
            }
        }
    }

    #endregion

    #region Oyun Akışı

    public async Task StartAsync(bool fastMode = false)
    {
        Console.Clear();
        PrintHeader();

        // 1. Taşları oluştur ve karıştır
        Console.WriteLine("\n🎲 Taşlar karıştırılıyor...");
        var allTiles = CreateTileSet();
        ShuffleTiles(allTiles);

        // 2. Göstergeyi belirle
        _indicatorTile = allTiles[0];
        allTiles.RemoveAt(0);
        Console.WriteLine($"📌 Gösterge: {FormatTile(_indicatorTile)}");
        Console.WriteLine($"🃏 Okey: {GetOkeyDescription()}");

        // 3. Taşları dağıt
        Console.WriteLine("\n🎴 Taşlar dağıtılıyor...");
        DealTiles(allTiles);

        // 4. Kalan taşları desteye koy
        foreach (var tile in allTiles)
        {
            _deck.Enqueue(tile);
        }
        Console.WriteLine($"📦 Destede {_deck.Count} taş kaldı.");

        // 5. Botları başlat
        foreach (var player in _players.Where(p => !p.IsHuman))
        {
            player.Bot!.Initialize(player.Hand, _indicatorTile);
        }

        // 6. İlk oyuncuyu seç (rastgele)
        _currentPlayerIndex = new Random().Next(4);
        
        // İlk oyuncu 15. taşı çeker
        if (_deck.Count > 0)
        {
            var firstTile = _deck.Dequeue();
            _players[_currentPlayerIndex].Hand.Add(firstTile);
            Console.WriteLine($"\n🎯 {_players[_currentPlayerIndex].Name} başlıyor! (15 taş)");
        }

        if (!fastMode)
        {
            Console.WriteLine("\n⏳ Oyun başlıyor... (Enter'a bas)");
            Console.ReadLine();
        }

        // 7. Oyun döngüsü
        await GameLoopAsync(fastMode);

        // 8. Sonuç
        PrintGameResult();
    }

    private void DealTiles(List<Tile> tiles)
    {
        // Her oyuncuya 14 taş
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 14; j++)
            {
                if (tiles.Count > 0)
                {
                    _players[i].Hand.Add(tiles[0]);
                    tiles.RemoveAt(0);
                }
            }
        }
    }

    private async Task GameLoopAsync(bool fastMode)
    {
        while (!_gameOver && _turnCount < MaxTurns && _deck.Count > 0)
        {
            _turnCount++;
            var currentPlayer = _players[_currentPlayerIndex];

            Console.WriteLine($"\n{'='.ToString().PadRight(50, '=')}");
            Console.WriteLine($"📍 Tur {_turnCount} | {currentPlayer.Name} | El: {currentPlayer.Hand.Count} taş | Deste: {_deck.Count}");

            if (currentPlayer.IsHuman)
            {
                await PlayHumanTurnAsync();
            }
            else
            {
                await PlayBotTurnAsync(currentPlayer, fastMode);
            }

            if (!_gameOver)
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % 4;
            }
        }

        if (_turnCount >= MaxTurns)
        {
            Console.WriteLine("\n⏰ Maksimum tur sayısına ulaşıldı!");
        }
        else if (_deck.Count == 0 && !_gameOver)
        {
            Console.WriteLine("\n📦 Deste bitti!");
        }
    }

    private async Task PlayBotTurnAsync(DemoPlayer player, bool fastMode)
    {
        var bot = player.Bot!;
        Tile? lastDiscard = _discardPile.Count > 0 ? _discardPile.Peek() : null;

        // 1. Çekme kararı
        var drawDecision = bot.DecideDrawSource(lastDiscard);
        
        if (!fastMode)
        {
            Console.WriteLine($"   🤔 Düşünüyor... ({drawDecision.ThinkingTimeMs / 1000.0:F1}s)");
            await Task.Delay(Math.Min(drawDecision.ThinkingTimeMs, 1000)); // Demo için kısaltılmış
        }

        Tile drawnTile;
        if (drawDecision.Type == BotDecisionType.DrawFromDiscard && lastDiscard != null)
        {
            drawnTile = _discardPile.Pop();
            Console.WriteLine($"   ⬆️ Discard'dan aldı: {FormatTile(drawnTile)}");
            
            // Diğer botlara bildir
            _botManager.NotifyPickup(drawnTile, player.Id);
        }
        else
        {
            drawnTile = _deck.Dequeue();
            Console.WriteLine($"   📥 Desteden çekti");
        }

        // 2. Atma kararı
        var discardDecision = bot.DecideDiscard(drawnTile);

        if (!fastMode)
        {
            await Task.Delay(Math.Min(discardDecision.ThinkingTimeMs, 500));
        }

        // 3. Kazanma kontrolü
        if (discardDecision.Type == BotDecisionType.DeclareWin)
        {
            Console.WriteLine($"   🎉 KAZANDI! {discardDecision.Reasoning}");
            _gameOver = true;
            _winner = player;
            return;
        }

        // 4. Taş at
        var discardedTile = discardDecision.Tile!;
        player.Hand.Remove(discardedTile);
        _discardPile.Push(discardedTile);
        Console.WriteLine($"   ⬇️ Attı: {FormatTile(discardedTile)}");

        // Diğer botlara bildir
        _botManager.NotifyDiscard(discardedTile, player.Id);
    }

    private async Task PlayHumanTurnAsync()
    {
        var player = _players[_currentPlayerIndex];
        
        // Eli göster
        Console.WriteLine("\n   📋 Senin elin:");
        PrintHand(player.Hand);

        // Çekme seçimi
        Tile? lastDiscard = _discardPile.Count > 0 ? _discardPile.Peek() : null;
        if (lastDiscard != null)
        {
            Console.WriteLine($"\n   📤 Son atılan: {FormatTile(lastDiscard)}");
            Console.Write("   [D]esteden çek veya [A]l (discard'dan): ");
        }
        else
        {
            Console.Write("   [D]esteden çek: ");
        }

        var drawChoice = Console.ReadLine()?.ToUpper() ?? "D";
        
        Tile drawnTile;
        if (drawChoice == "A" && lastDiscard != null)
        {
            drawnTile = _discardPile.Pop();
            Console.WriteLine($"   ⬆️ Aldın: {FormatTile(drawnTile)}");
        }
        else
        {
            drawnTile = _deck.Dequeue();
            Console.WriteLine($"   📥 Çektin: {FormatTile(drawnTile)}");
        }

        player.Hand.Add(drawnTile);

        // Kazanma kontrolü
        var winCheck = _ruleEngine.CheckWinningHand(player.Hand);
        if (winCheck.IsWinning)
        {
            Console.WriteLine($"\n   🎉 KAZANABİLİRSİN! {winCheck.WinType}");
            Console.Write("   [K]azan veya [D]evam: ");
            var winChoice = Console.ReadLine()?.ToUpper() ?? "D";
            
            if (winChoice == "K")
            {
                _gameOver = true;
                _winner = player;
                return;
            }
        }

        // Atma seçimi
        Console.WriteLine("\n   📋 Güncel elin:");
        PrintHandWithIndex(player.Hand);
        
        Console.Write("   Hangi taşı atacaksın (numara): ");
        var discardInput = Console.ReadLine() ?? "0";
        
        if (int.TryParse(discardInput, out int index) && index >= 0 && index < player.Hand.Count)
        {
            var discardedTile = player.Hand[index];
            player.Hand.RemoveAt(index);
            _discardPile.Push(discardedTile);
            Console.WriteLine($"   ⬇️ Attın: {FormatTile(discardedTile)}");
        }
        else
        {
            // Geçersiz giriş, rastgele at
            var randomIndex = new Random().Next(player.Hand.Count);
            var discardedTile = player.Hand[randomIndex];
            player.Hand.RemoveAt(randomIndex);
            _discardPile.Push(discardedTile);
            Console.WriteLine($"   ⬇️ (Rastgele) Attın: {FormatTile(discardedTile)}");
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Görüntüleme

    private void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                    🎮 OKEY OYUNU DEMO 🎮                      ║
║                   Akıllı Bot AI Sistemi                       ║
╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    private void PrintGameResult()
    {
        Console.WriteLine("\n" + new string('═', 60));
        
        if (_winner != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n🏆 KAZANAN: {_winner.Name}");
            Console.ResetColor();
            
            Console.WriteLine("\n📊 Son durum:");
            foreach (var player in _players)
            {
                string status = player == _winner ? "🏆" : "❌";
                Console.WriteLine($"   {status} {player.Name}: {player.Hand.Count} taş");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n⏸️ OYUN BİTTİ - Kazanan yok!");
            Console.ResetColor();
        }

        Console.WriteLine($"\n📈 Toplam tur: {_turnCount}");
        Console.WriteLine($"📦 Destede kalan: {_deck.Count} taş");
    }

    private string FormatTile(Tile tile)
    {
        if (tile.IsFalseJoker)
        {
            return "🃏 SahteOkey";
        }

        string colorEmoji = tile.Color switch
        {
            TileColor.Yellow => "🟡",
            TileColor.Blue => "🔵",
            TileColor.Black => "⚫",
            TileColor.Red => "🔴",
            _ => "⬜"
        };

        string okeyMark = tile.IsOkey ? "★" : "";
        return $"{colorEmoji}{tile.Value}{okeyMark}";
    }

    private string GetOkeyDescription()
    {
        int okeyValue = _indicatorTile.Value == 13 ? 1 : _indicatorTile.Value + 1;
        string colorEmoji = _indicatorTile.Color switch
        {
            TileColor.Yellow => "🟡",
            TileColor.Blue => "🔵",
            TileColor.Black => "⚫",
            TileColor.Red => "🔴",
            _ => "⬜"
        };
        return $"{colorEmoji}{okeyValue}";
    }

    private void PrintHand(List<Tile> hand)
    {
        var sorted = hand.OrderBy(t => t.Color).ThenBy(t => t.Value).ToList();
        var groups = sorted.GroupBy(t => t.Color);

        foreach (var group in groups)
        {
            Console.Write("      ");
            foreach (var tile in group)
            {
                Console.Write($"{FormatTile(tile)} ");
            }
            Console.WriteLine();
        }
    }

    private void PrintHandWithIndex(List<Tile> hand)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            Console.WriteLine($"      [{i}] {FormatTile(hand[i])}");
        }
    }

    #endregion
}

/// <summary>
/// Demo oyuncu sınıfı.
/// </summary>
public class DemoPlayer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsHuman { get; set; }
    public OkeyBotAI? Bot { get; set; }
    public int SeatIndex { get; set; }
    public List<Tile> Hand { get; set; } = new();
}
