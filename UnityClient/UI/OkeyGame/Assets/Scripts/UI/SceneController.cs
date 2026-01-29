using UnityEngine;
using UnityEngine.UIElements;
using OkeyGame.Core;

namespace OkeyGame.UI
{
    /// <summary>
    /// Ana sahne controller - UI ekranları arasında geçişi yönetir
    /// </summary>
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [Header("UI Documents")]
        [SerializeField] private UIDocument _mainMenuDocument;
        [SerializeField] private UIDocument _lobbyDocument;
        [SerializeField] private UIDocument _gameTableDocument;

        [Header("Screen Controllers")]
        [SerializeField] private MainMenuScreen _mainMenuScreen;
        [SerializeField] private LobbyScreen _lobbyScreen;
        [SerializeField] private GameTableScreen _gameTableScreen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Auto-find UI Documents if not assigned
            AutoFindUIDocuments();
        }

        private void AutoFindUIDocuments()
        {
            if (_mainMenuDocument == null)
            {
                var mainMenuGO = GameObject.Find("MainMenuUI");
                if (mainMenuGO != null)
                {
                    _mainMenuDocument = mainMenuGO.GetComponent<UIDocument>();
                    _mainMenuScreen = mainMenuGO.GetComponent<MainMenuScreen>();
                }
            }
            
            if (_lobbyDocument == null)
            {
                var lobbyGO = GameObject.Find("LobbyUI");
                if (lobbyGO != null)
                {
                    _lobbyDocument = lobbyGO.GetComponent<UIDocument>();
                    _lobbyScreen = lobbyGO.GetComponent<LobbyScreen>();
                }
            }
            
            if (_gameTableDocument == null)
            {
                var gameTableGO = GameObject.Find("GameTableUI");
                if (gameTableGO != null)
                {
                    _gameTableDocument = gameTableGO.GetComponent<UIDocument>();
                    _gameTableScreen = gameTableGO.GetComponent<GameTableScreen>();
                }
            }
            
            Debug.Log($"[SceneController] Auto-found UI Documents - MainMenu: {_mainMenuDocument != null}, Lobby: {_lobbyDocument != null}, GameTable: {_gameTableDocument != null}");
        }

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Start()
        {
            // Başlangıçta MainMenu göster
            ShowMainMenu();
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:
                case GameState.Login:
                    ShowMainMenu();
                    break;

                case GameState.Lobby:
                case GameState.InRoom:
                    ShowLobby();
                    break;

                case GameState.Playing:
                    ShowGameTable();
                    break;

                case GameState.GameOver:
                    // Oyun tablosu açık kalır, modal gösterilir
                    break;
            }
        }

        public void ShowMainMenu()
        {
            SetScreenActive(_mainMenuDocument, _mainMenuScreen, true);
            SetScreenActive(_lobbyDocument, _lobbyScreen, false);
            SetScreenActive(_gameTableDocument, _gameTableScreen, false);
        }

        public void ShowLobby()
        {
            SetScreenActive(_mainMenuDocument, _mainMenuScreen, false);
            SetScreenActive(_lobbyDocument, _lobbyScreen, true);
            SetScreenActive(_gameTableDocument, _gameTableScreen, false);
        }

        public void ShowGameTable()
        {
            SetScreenActive(_mainMenuDocument, _mainMenuScreen, false);
            SetScreenActive(_lobbyDocument, _lobbyScreen, false);
            SetScreenActive(_gameTableDocument, _gameTableScreen, true);
        }

        private void SetScreenActive(UIDocument document, MonoBehaviour screen, bool active)
        {
            if (document != null)
            {
                document.gameObject.SetActive(active);
            }
            
            if (screen != null && screen.gameObject != document?.gameObject)
            {
                screen.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Loading overlay göster
        /// </summary>
        public void ShowLoading(string message = "Yükleniyor...")
        {
            _mainMenuScreen?.ShowLoading(message);
        }

        /// <summary>
        /// Loading overlay gizle
        /// </summary>
        public void HideLoading()
        {
            _mainMenuScreen?.HideLoading();
        }
    }
}
