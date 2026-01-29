using UnityEngine;
using UnityEngine.UIElements;
using OkeyGame.Core;
using OkeyGame.Network;

using GameState = OkeyGame.Core.GameState;

namespace OkeyGame.UI
{
    /// <summary>
    /// Ana menü ekranı controller
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private bool _demoMode = true; // Backend olmadan test için

        private VisualElement _root;
        private Button _playButton;
        private Button _guestButton;
        private Button _loginButton;
        private Button _settingsButton;
        private VisualElement _playerInfoPanel;
        private Label _playerName;
        private Label _playerChips;
        private Label _playerElo;
        private VisualElement _loadingOverlay;
        private Label _loadingText;

        private void OnEnable()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }
            
            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[MainMenu] UIDocument bulunamadı!");
                return;
            }
            
            _root = _uiDocument.rootVisualElement;

            // Get UI elements - null check ile
            _playButton = _root.Q<Button>("play-button");
            _guestButton = _root.Q<Button>("guest-button");
            _loginButton = _root.Q<Button>("login-button");
            _settingsButton = _root.Q<Button>("settings-button");
            _playerInfoPanel = _root.Q<VisualElement>("player-info-panel");
            _playerName = _root.Q<Label>("player-name");
            _playerChips = _root.Q<Label>("player-chips");
            _playerElo = _root.Q<Label>("player-elo");
            _loadingOverlay = _root.Q<VisualElement>("loading-overlay");
            _loadingText = _root.Q<Label>("loading-text");

            // Register callbacks
            if (_playButton != null) _playButton.clicked += OnPlayClicked;
            if (_guestButton != null) _guestButton.clicked += OnGuestLoginClicked;
            if (_loginButton != null) _loginButton.clicked += OnLoginClicked;
            if (_settingsButton != null) _settingsButton.clicked += OnSettingsClicked;

            // Subscribe to events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerInfoUpdated += UpdatePlayerInfo;
            }

            // Initial UI state
            UpdateUI();
            
            Debug.Log("[MainMenu] UI initialized successfully!");
        }

        private void OnDisable()
        {
            // Unregister callbacks
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            if (_guestButton != null) _guestButton.clicked -= OnGuestLoginClicked;
            if (_loginButton != null) _loginButton.clicked -= OnLoginClicked;
            if (_settingsButton != null) _settingsButton.clicked -= OnSettingsClicked;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerInfoUpdated -= UpdatePlayerInfo;
            }
        }

        private void UpdateUI()
        {
            var isLoggedIn = ApiService.Instance?.IsAuthenticated == true;

            // Show/hide buttons based on login state
            _guestButton.style.display = isLoggedIn ? DisplayStyle.None : DisplayStyle.Flex;
            _loginButton.text = isLoggedIn ? "Cikis Yap" : "Giris / Kayit";

            // Show player info if logged in
            _playerInfoPanel.style.display = isLoggedIn ? DisplayStyle.Flex : DisplayStyle.None;

            if (isLoggedIn)
            {
                UpdatePlayerInfo();
            }
        }

        private void UpdatePlayerInfo()
        {
            if (GameManager.Instance == null) return;

            _playerName.text = GameManager.Instance.PlayerName;
            _playerChips.text = $"{GameManager.Instance.PlayerChips:N0} Chip";
            _playerElo.text = $"ELO: {GameManager.Instance.PlayerElo}";
        }

        private async void OnPlayClicked()
        {
            Debug.Log("[MainMenu] Play clicked");

            ShowLoading("Sunucuya bağlanılıyor...");
            
            try
            {
                // Demo oyuncu bilgisi ayarla
                GameManager.Instance?.SetPlayerInfo("demo-player-" + System.Guid.NewGuid().ToString().Substring(0, 8), "Oyuncu", 10000, 1200);
                
                var hubUrl = GameSettings.Instance?.SignalRHubUrl ?? "http://localhost:57392/gamehub";
                Debug.Log($"[MainMenu] Connecting to: {hubUrl}");
                
                if (SignalRConnection.Instance == null)
                {
                    HideLoading();
                    ShowError("SignalR servisi hazır değil!");
                    return;
                }
                
                var connected = await SignalRConnection.Instance.ConnectAsync(hubUrl, null);

                HideLoading();

                if (connected)
                {
                    Debug.Log("[MainMenu] SignalR connected, going to lobby");
                    GameManager.Instance?.ChangeState(GameState.Lobby);
                }
                else
                {
                    Debug.LogWarning("[MainMenu] SignalR connection failed, going to lobby anyway (demo mode)");
                    // Demo modda bağlantı başarısız olsa bile lobiye git
                    GameManager.Instance?.ChangeState(GameState.Lobby);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MainMenu] Connection error: {ex.Message}");
                HideLoading();
                // Hata olsa bile lobiye git (demo mode)
                GameManager.Instance?.ChangeState(GameState.Lobby);
            }
        }

        private async void OnGuestLoginClicked()
        {
            // Demo mode
            if (_demoMode)
            {
                Debug.Log("[MainMenu] Demo mode - Misafir giriş");
                GameManager.Instance?.SetPlayerInfo("guest-demo", "Misafir", 5000, 1000);
                UpdateUI();
                return;
            }
            
            await OnGuestLoginAsync();
        }

        private async System.Threading.Tasks.Task OnGuestLoginAsync()
        {
            if (ApiService.Instance == null)
            {
                ShowError("API servisi henüz hazır değil!");
                return;
            }

            ShowLoading("Giriş yapılıyor...");

            var response = await ApiService.Instance.GuestLoginAsync();

            HideLoading();

            if (response.Success)
            {
                Debug.Log($"[MainMenu] Guest login successful: {response.Data.Username}");
                UpdateUI();
            }
            else
            {
                ShowError($"Giriş başarısız: {response.Error}");
            }
        }

        private void OnLoginClicked()
        {
            if (ApiService.Instance == null) return;
            
            if (ApiService.Instance.IsAuthenticated)
            {
                // Logout
                ApiService.Instance.Logout();
                UpdateUI();
            }
            else
            {
                // Show login modal
                Debug.Log("[MainMenu] Show login modal");
                // TODO: Open login/register modal
            }
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MainMenu] Settings clicked");
            // TODO: Open settings modal
        }

        public void ShowLoading(string message)
        {
            if (_loadingText != null) _loadingText.text = message;
            if (_loadingOverlay != null)
            {
                _loadingOverlay.style.display = DisplayStyle.Flex;
                _loadingOverlay.AddToClassList("visible");
            }
        }

        public void HideLoading()
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.style.display = DisplayStyle.None;
                _loadingOverlay.RemoveFromClassList("visible");
            }
        }

        private void ShowError(string message)
        {
            Debug.LogError($"[MainMenu] {message}");
            
            // Error toast göster
            var errorToast = _root.Q<VisualElement>("error-toast");
            var errorText = _root.Q<Label>("error-text");
            
            if (errorToast != null && errorText != null)
            {
                errorText.text = message;
                errorToast.style.display = DisplayStyle.Flex;
                errorToast.AddToClassList("visible");
                
                // 3 saniye sonra gizle
                StartCoroutine(HideErrorAfterDelay(errorToast, 3f));
            }
        }

        private System.Collections.IEnumerator HideErrorAfterDelay(VisualElement toast, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (toast != null)
            {
                toast.style.display = DisplayStyle.None;
                toast.RemoveFromClassList("visible");
            }
        }
    }
}
