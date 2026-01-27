using OkeyGame.Demo;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                    🎮 OKEY OYUNU DEMO 🎮                      ║
║                   Akıllı Bot AI Sistemi                       ║
╚═══════════════════════════════════════════════════════════════╝
");
Console.ResetColor();

Console.WriteLine("Oyun modunu seç:");
Console.WriteLine("  [1] 👁️ İzle - 4 Bot birbiriyle oynasın (hızlı)");
Console.WriteLine("  [2] 👁️ İzle - 4 Bot birbiriyle oynasın (yavaş)");
Console.WriteLine("  [3] 🎮 Oyna - Sen vs 3 Bot");
Console.WriteLine();
Console.Write("Seçimin (1/2/3): ");

var choice = Console.ReadLine() ?? "1";

bool humanPlayer = choice == "3";
bool fastMode = choice == "1";

var demo = new GameDemo(humanPlayer);
await demo.StartAsync(fastMode);

Console.WriteLine("\n\nTekrar oynamak için Enter'a bas, çıkmak için Q yaz:");
var replay = Console.ReadLine()?.ToUpper();

while (replay != "Q")
{
    demo = new GameDemo(humanPlayer);
    await demo.StartAsync(fastMode);
    
    Console.WriteLine("\n\nTekrar oynamak için Enter'a bas, çıkmak için Q yaz:");
    replay = Console.ReadLine()?.ToUpper();
}

Console.WriteLine("👋 Görüşmek üzere!");
