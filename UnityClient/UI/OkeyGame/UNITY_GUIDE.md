# 🎮 Unity Okey Oyunu - Başlangıç Rehberi

Bu rehber, Unity bilginiz sıfır bile olsa Okey oyunu projesini nasıl çalıştıracağınızı ve geliştireceğinizi adım adım açıklar.

---

## 📋 İçindekiler

1. [Proje Yapısı](#-proje-yapısı)
2. [Unity Hub Kurulumu](#-unity-hub-kurulumu)
3. [Projeyi Açma](#-projeyi-açma)
4. [Sahne Kurulumu](#-sahne-kurulumu)
5. [Oyunu Çalıştırma](#-oyunu-çalıştırma)
6. [Mobil Derleme](#-mobil-derleme)
7. [Kod Mimarisi](#-kod-mimarisi)
8. [Sık Sorulan Sorular](#-sık-sorulan-sorular)

---

## 📁 Proje Yapısı

```
Assets/
├── Scripts/
│   ├── Core/           # Temel sistem scriptleri
│   │   ├── GameManager.cs      # Oyun durumu yönetimi
│   │   ├── GameSettings.cs     # Ayarlar (ScriptableObject)
│   │   └── GameBootstrap.cs    # Başlatıcı script
│   │
│   ├── Models/         # Veri modelleri
│   │   └── GameModels.cs       # OkeyTile, PlayerInfo, RoomInfo vb.
│   │
│   ├── Network/        # Ağ iletişimi
│   │   ├── ApiService.cs       # REST API client
│   │   ├── SignalRConnection.cs # WebSocket real-time bağlantı
│   │   └── WebSocketClient.cs  # WebSocket wrapper
│   │
│   ├── Game/           # Oyun mantığı
│   │   └── GameTableController.cs # Oyun masası kontrolü
│   │
│   └── UI/             # Kullanıcı arayüzü
│       ├── MainMenuScreen.cs   # Ana menü
│       ├── LobbyScreen.cs      # Oda listesi
│       ├── GameTableScreen.cs  # Oyun masası
│       └── SceneController.cs  # Ekran geçişleri
│
├── UI/
│   ├── Documents/      # UXML dosyaları (UI layout)
│   │   ├── MainMenuScreen.uxml
│   │   ├── LobbyScreen.uxml
│   │   └── GameTableScreen.uxml
│   │
│   └── Styles/         # USS dosyaları (CSS benzeri stiller)
│       ├── MainMenuStyles.uss
│       ├── LobbyStyles.uss
│       └── GameTableStyles.uss
│
└── Settings/           # Ayar dosyaları
    └── GameSettings.asset
```

---

## 🔧 Unity Hub Kurulumu

### Adım 1: Unity Hub İndir
1. https://unity.com/download adresine gidin
2. "Download Unity Hub" butonuna tıklayın
3. İndirilen dosyayı çalıştırın ve kurulumu tamamlayın

### Adım 2: Unity Editör Kur
1. Unity Hub'ı açın
2. Sol menüden "Installs" seçin
3. "Install Editor" butonuna tıklayın
4. **Unity 2022.3 LTS** veya üstü bir sürüm seçin
5. Modülleri seçin:
   - ✅ **Android Build Support** (mobil için gerekli)
   - ✅ **iOS Build Support** (iOS için gerekli, sadece Mac'te)
   - ✅ **WebGL Build Support** (web versiyonu için)
6. "Install" butonuna tıklayın ve bekleyin

---

## 📂 Projeyi Açma

### Adım 1: Unity Hub'da Projeyi Ekle
1. Unity Hub'ı açın
2. "Projects" sekmesine gidin
3. "Add" butonuna tıklayın
4. `UnityClient/UI/OkeyGame` klasörünü seçin
5. Proje listede görünecek

### Adım 2: Projeyi Aç
1. Proje ismine tıklayın
2. Unity Editör açılacak (ilk açılış 2-5 dakika sürebilir)
3. Console panelinde hata olmadığından emin olun

---

## 🎬 Sahne Kurulumu

Oyunun çalışması için sahneyi doğru kurmanız gerekiyor.

### Adım 1: GameSettings Oluştur

1. **Project** panelinde `Assets/Settings` klasörü oluşturun:
   - Project panelinde sağ tık → Create → Folder
   - İsim: `Settings`

2. GameSettings asset oluşturun:
   - Settings klasörüne sağ tık → Create → Okey Game → Game Settings
   - İsim: `GameSettings`

3. Ayarları düzenleyin:
   - Oluşan `GameSettings` dosyasına tıklayın
   - Inspector panelinde:
     - **Server Url**: `https://localhost:7001` (Backend adresi)
     - **SignalR Hub Path**: `/gamehub`
     - **Connection Timeout**: `30`
     - **Turn Timeout Seconds**: `60`

### Adım 2: Sahne Oluştur

1. Yeni sahne oluşturun:
   - File → New Scene
   - "Basic 2D (Built-in)" seçin

2. Sahneyi kaydedin:
   - File → Save As
   - `Assets/Scenes` klasörü oluşturun
   - İsim: `MainScene`

### Adım 3: Bootstrap GameObject Ekle

1. **Hierarchy** panelinde sağ tık → Create Empty
2. İsim: `Bootstrap`
3. Inspector'da **Add Component** → Scripts → OkeyGame → Core → **GameBootstrap**
4. GameSettings alanına, oluşturduğunuz `GameSettings` asset'i sürükleyin

### Adım 4: UI Document Ekle (Ana Menü)

1. Hierarchy'de sağ tık → UI Toolkit → **UI Document**
2. İsim: `MainMenuUI`
3. Inspector'da:
   - **Source Asset**: `Assets/UI/Documents/MainMenuScreen.uxml` seçin
4. **Add Component** → Scripts → OkeyGame → UI → **MainMenuScreen**
5. UI Document alanına kendisini sürükleyin (otomatik atanmış olabilir)

### Adım 5: UI Document Ekle (Oyun Masası)

1. Hierarchy'de sağ tık → UI Toolkit → **UI Document**
2. İsim: `GameTableUI`
3. Inspector'da:
   - **Source Asset**: `Assets/UI/Documents/GameTableScreen.uxml` seçin
4. **Add Component** → Scripts → OkeyGame → UI → **GameTableScreen**
5. Başlangıçta devre dışı: Inspector'da GameObject isminin yanındaki ☑️ işaretini kaldırın

### Adım 6: Scene Controller Ekle

1. Hierarchy'de sağ tık → Create Empty
2. İsim: `SceneController`
3. **Add Component** → Scripts → OkeyGame → UI → **SceneController**
4. Inspector'da:
   - **Main Menu Document**: `MainMenuUI` GameObject'i sürükleyin
   - **Game Table Document**: `GameTableUI` GameObject'i sürükleyin

---

## ▶️ Oyunu Çalıştırma

### Backend'i Başlat

1. Visual Studio veya terminal'de Backend projesini çalıştırın:
```bash
cd Backend
dotnet run
```

2. Backend'in çalıştığını doğrulayın:
   - Tarayıcıda: `https://localhost:7001/swagger`

### Unity'de Test Et

1. Unity Editör'de **Play** butonuna (▶️) tıklayın
2. Ana menü görünecek:
   - "Misafir Olarak Giriş" ile giriş yapın
   - "Oyna" ile lobiye gidin
3. Console panelinde hataları kontrol edin

---

## 📱 Mobil Derleme

### Android için

1. **File → Build Settings**
2. Sol listeden **Android** seçin
3. **Switch Platform** tıklayın (ilk seferinde uzun sürebilir)
4. **Player Settings** ayarları:
   - Company Name: Şirket adınız
   - Product Name: "Okey Oyunu"
   - Package Name: `com.sirketadi.okeyoyunu`
   - Minimum API Level: **API Level 24** (Android 7.0)
5. **Build** tıklayın
6. APK dosyasını kaydedin

### iOS için (Sadece Mac)

1. **File → Build Settings**
2. Sol listeden **iOS** seçin
3. **Switch Platform** tıklayın
4. **Player Settings** ayarları:
   - Bundle Identifier: `com.sirketadi.okeyoyunu`
5. **Build** tıklayın
6. Xcode projesi oluşturulacak
7. Xcode'da açıp derleyin

---

## 🏗️ Kod Mimarisi

### Singleton Pattern

Manager'lar tek örnek (singleton) olarak çalışır:

```csharp
// Herhangi bir yerden erişim:
GameManager.Instance.PlayerName;
ApiService.Instance.LoginAsync();
SignalRConnection.Instance.JoinRoom("room-id");
```

### Event-Driven Mimari

Değişiklikler event'ler ile bildiriliyor:

```csharp
// Event'e abone ol
GameManager.OnGameStateChanged += HandleStateChange;

// Event handler
void HandleStateChange(GameState newState) {
    Debug.Log($"Yeni durum: {newState}");
}
```

### State Machine

Oyun durumları:

```
MainMenu → Login → Lobby → InRoom → Playing → GameOver
     ↑                                            │
     └────────────────────────────────────────────┘
```

### UI Toolkit

Modern CSS benzeri UI sistemi:

- **UXML**: HTML benzeri yapı (layout)
- **USS**: CSS benzeri stiller
- **C#**: Controller script

```csharp
// UI elementine erişim
var button = _root.Q<Button>("play-button");
button.clicked += OnPlayClicked;

// Stil değiştir
button.AddToClassList("selected");
button.RemoveFromClassList("disabled");
```

---

## 🔧 Özelleştirme

### Renkleri Değiştir

`Assets/UI/Styles/` klasöründeki USS dosyalarını düzenleyin:

```css
/* MainMenuStyles.uss */
.main-container {
    background-color: rgb(25, 90, 50); /* Yeşil arka plan */
}

.play-button {
    background-color: rgb(255, 193, 7); /* Sarı buton */
}
```

### Taş Görsellerini Değiştir

`GameTableScreen.cs` dosyasında `CreateTileElement` metodunu düzenleyin:

```csharp
private VisualElement CreateTileElement(OkeyTile tile) {
    var element = new VisualElement();
    element.AddToClassList("tile");
    
    // Özel görsel ekle
    element.style.backgroundImage = new StyleBackground(tileSprite);
    
    return element;
}
```

### Sunucu Adresini Değiştir

`GameSettings` ScriptableObject'te **Server Url** alanını düzenleyin.

---

## ❓ Sık Sorulan Sorular

### Q: Console'da "namespace not found" hatası alıyorum

**A**: Tüm script dosyalarının doğru klasörlerde olduğundan emin olun ve Unity'yi yeniden başlatın.

### Q: UI görünmüyor

**A**: 
1. UIDocument'in Source Asset'inin atandığından emin olun
2. Panel Settings oluşturun: Create → UI Toolkit → Panel Settings
3. UIDocument'e atayın

### Q: Backend'e bağlanamıyor

**A**:
1. Backend'in çalıştığından emin olun
2. GameSettings'te doğru URL'i kontrol edin
3. Firewall'u kontrol edin
4. HTTPS sertifika uyarılarını kabul edin

### Q: Android'de çalışmıyor

**A**:
1. `android:usesCleartextTraffic="true"` - HTTP için gerekli
2. Internet izni: Player Settings → Other Settings → Internet Access: Require
3. Sunucu IP'sini localhost yerine gerçek IP ile değiştirin

### Q: Oyun donuyor

**A**: Console'daki hata mesajlarını kontrol edin. Genellikle null reference veya network timeout sorunlarıdır.

---

## 📚 Ek Kaynaklar

- [Unity UI Toolkit Manual](https://docs.unity3d.com/Manual/UIElements.html)
- [Unity Learn](https://learn.unity.com/)
- [C# Fundamentals](https://docs.microsoft.com/en-us/dotnet/csharp/)

---

## 🎯 Sonraki Adımlar

1. ✅ Projeyi çalıştırın
2. 📝 UI'ı kendi tasarımınıza göre düzenleyin
3. 🎨 Taş görselleri ekleyin
4. 🔊 Ses efektleri ekleyin
5. 📱 Mobil test yapın
6. 🚀 Yayınlayın!

---

Sorularınız için destek alabilirsiniz. İyi kodlamalar! 🎮
