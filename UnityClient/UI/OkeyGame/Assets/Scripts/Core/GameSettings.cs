using System;
using UnityEngine;

namespace OkeyGame.Core
{
    /// <summary>
    /// Uygulama ayarları - Backend URL, timeout gibi değerler
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "OkeyGame/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        public static GameSettings Instance { get; set; }

        [Header("Server Settings")]
        [SerializeField] private string _serverUrl = "http://localhost:57392";
        [SerializeField] private string _signalRHubPath = "/gamehub";
        [SerializeField] private float _connectionTimeout = 30f;
        [SerializeField] private float _reconnectDelay = 2f;
        [SerializeField] private int _maxReconnectAttempts = 5;

        [Header("Game Settings")]
        [SerializeField] private float _turnTimeoutSeconds = 30f;
        [SerializeField] private float _autoPlayWarningSeconds = 5f;
        [SerializeField] private bool _enableSoundEffects = true;
        [SerializeField] private bool _enableVibration = true;
        [SerializeField] private bool _enableNotifications = true;

        [Header("UI Settings")]
        [SerializeField] private float _animationSpeed = 1f;
        [SerializeField] private bool _showTileNumbers = true;
        [SerializeField] private string _tableTheme = "classic";

        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;
        [SerializeField] private bool _logNetworkMessages = false;

        // Properties
        public string ServerUrl => _serverUrl;
        public string SignalRHubUrl => $"{_serverUrl}{_signalRHubPath}";
        public float ConnectionTimeout => _connectionTimeout;
        public float ReconnectDelay => _reconnectDelay;
        public int MaxReconnectAttempts => _maxReconnectAttempts;

        public float TurnTimeoutSeconds => _turnTimeoutSeconds;
        public float AutoPlayWarningSeconds => _autoPlayWarningSeconds;
        public bool EnableSoundEffects => _enableSoundEffects;
        public bool EnableVibration => _enableVibration;
        public bool EnableNotifications => _enableNotifications;

        public float AnimationSpeed => _animationSpeed;
        public bool ShowTileNumbers => _showTileNumbers;
        public string TableTheme => _tableTheme;

        public bool DebugMode => _debugMode;
        public bool LogNetworkMessages => _logNetworkMessages;

        private void OnEnable()
        {
            Instance = this;
        }

        public void SetServerUrl(string url)
        {
            _serverUrl = url;
        }

        public void SetDebugMode(bool enabled)
        {
            _debugMode = enabled;
            _logNetworkMessages = enabled;
        }
    }
}
