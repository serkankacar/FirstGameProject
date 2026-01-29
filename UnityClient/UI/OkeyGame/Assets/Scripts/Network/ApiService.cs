using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using OkeyGame.Models;
using OkeyGame.Core;

namespace OkeyGame.Network
{
    /// <summary>
    /// REST API servisi - Login, Register, Leaderboard gibi işlemler
    /// </summary>
    public class ApiService : MonoBehaviour
    {
        public static ApiService Instance { get; private set; }

        private string _authToken;
        private DateTime _tokenExpiry;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiry;
        public string AuthToken => _authToken;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load cached token
            LoadToken();
        }

        #region Authentication

        public async Task<ApiResponse<LoginResponse>> LoginAsync(string username, string password)
        {
            var request = new { Username = username, Password = password };
            var response = await PostAsync<LoginResponse>("/api/auth/login", request);

            if (response.Success)
            {
                SetToken(response.Data.Token, response.Data.TokenExpiry);
                GameManager.Instance.SetPlayerInfo(
                    response.Data.PlayerId,
                    response.Data.Username,
                    response.Data.Chips,
                    response.Data.EloScore
                );
            }

            return response;
        }

        public async Task<ApiResponse<LoginResponse>> RegisterAsync(string username, string email, string password)
        {
            var request = new { Username = username, Email = email, Password = password };
            var response = await PostAsync<LoginResponse>("/api/auth/register", request);

            if (response.Success)
            {
                SetToken(response.Data.Token, response.Data.TokenExpiry);
                GameManager.Instance.SetPlayerInfo(
                    response.Data.PlayerId,
                    response.Data.Username,
                    response.Data.Chips,
                    response.Data.EloScore
                );
            }

            return response;
        }

        public async Task<ApiResponse<LoginResponse>> GuestLoginAsync()
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            var request = new { DeviceId = deviceId };
            var response = await PostAsync<LoginResponse>("/api/auth/guest", request);

            if (response.Success)
            {
                SetToken(response.Data.Token, response.Data.TokenExpiry);
                GameManager.Instance.SetPlayerInfo(
                    response.Data.PlayerId,
                    response.Data.Username,
                    response.Data.Chips,
                    response.Data.EloScore
                );
            }

            return response;
        }

        public void Logout()
        {
            _authToken = null;
            _tokenExpiry = DateTime.MinValue;
            PlayerPrefs.DeleteKey("AuthToken");
            PlayerPrefs.DeleteKey("TokenExpiry");
            PlayerPrefs.Save();
            
            GameManager.Instance.ClearPlayerInfo();
            GameManager.Instance.ChangeState(Core.GameState.MainMenu);
        }

        private void SetToken(string token, DateTime expiry)
        {
            _authToken = token;
            _tokenExpiry = expiry;
            PlayerPrefs.SetString("AuthToken", token);
            PlayerPrefs.SetString("TokenExpiry", expiry.ToString("O"));
            PlayerPrefs.Save();
        }

        private void LoadToken()
        {
            _authToken = PlayerPrefs.GetString("AuthToken", "");
            var expiryStr = PlayerPrefs.GetString("TokenExpiry", "");
            
            if (!string.IsNullOrEmpty(expiryStr) && DateTime.TryParse(expiryStr, out DateTime expiry))
            {
                _tokenExpiry = expiry;
            }
        }

        #endregion

        #region Room Operations

        public async Task<ApiResponse<RoomListResponse>> GetRoomsAsync(long? minStake = null, long? maxStake = null)
        {
            var url = "/api/rooms";
            if (minStake.HasValue || maxStake.HasValue)
            {
                url += "?";
                if (minStake.HasValue) url += $"minStake={minStake}&";
                if (maxStake.HasValue) url += $"maxStake={maxStake}";
            }
            return await GetAsync<RoomListResponse>(url);
        }

        public async Task<ApiResponse<RoomInfo>> CreateRoomAsync(long tableStake, int? minElo = null, int? maxElo = null)
        {
            var request = new { TableStake = tableStake, MinElo = minElo, MaxElo = maxElo };
            return await PostAsync<RoomInfo>("/api/rooms", request);
        }

        public async Task<ApiResponse<RoomInfo>> GetRoomAsync(string roomId)
        {
            return await GetAsync<RoomInfo>($"/api/rooms/{roomId}");
        }

        #endregion

        #region Leaderboard

        public async Task<ApiResponse<LeaderboardEntry[]>> GetLeaderboardAsync(int top = 100)
        {
            return await GetAsync<LeaderboardEntry[]>($"/api/leaderboard?top={top}");
        }

        public async Task<ApiResponse<LeaderboardEntry>> GetMyRankAsync()
        {
            return await GetAsync<LeaderboardEntry>("/api/leaderboard/me");
        }

        #endregion

        #region Player

        public async Task<ApiResponse<PlayerInfo>> GetProfileAsync()
        {
            return await GetAsync<PlayerInfo>("/api/player/profile");
        }

        public async Task<ApiResponse<object>> UpdateProfileAsync(string displayName, string avatarUrl)
        {
            var request = new { DisplayName = displayName, AvatarUrl = avatarUrl };
            return await PutAsync<object>("/api/player/profile", request);
        }

        #endregion

        #region HTTP Helpers

        private async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            var url = GameSettings.Instance.ServerUrl + endpoint;
            
            using (var request = UnityWebRequest.Get(url))
            {
                AddAuthHeader(request);
                request.timeout = (int)GameSettings.Instance.ConnectionTimeout;

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                return ProcessResponse<T>(request);
            }
        }

        private async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data)
        {
            var url = GameSettings.Instance.ServerUrl + endpoint;
            var json = JsonUtility.ToJson(data);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                AddAuthHeader(request);
                request.timeout = (int)GameSettings.Instance.ConnectionTimeout;

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                return ProcessResponse<T>(request);
            }
        }

        private async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object data)
        {
            var url = GameSettings.Instance.ServerUrl + endpoint;
            var json = JsonUtility.ToJson(data);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                AddAuthHeader(request);
                request.timeout = (int)GameSettings.Instance.ConnectionTimeout;

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                return ProcessResponse<T>(request);
            }
        }

        private void AddAuthHeader(UnityWebRequest request)
        {
            if (IsAuthenticated)
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            }
        }

        private ApiResponse<T> ProcessResponse<T>(UnityWebRequest request)
        {
            var response = new ApiResponse<T>();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var json = request.downloadHandler.text;
                    Debug.Log($"[API] Response JSON: {json}");
                    
                    // Backend doğrudan data dönüyor, ApiResponse wrapper kullanmıyor
                    // Bu yüzden doğrudan T olarak parse ediyoruz
                    response.Data = JsonUtility.FromJson<T>(json);
                    response.Success = true;
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Error = $"Parse error: {ex.Message}";
                    Debug.LogError($"[API] Parse error: {ex.Message}");
                }
            }
            else
            {
                response.Success = false;
                response.Error = request.error ?? $"HTTP {request.responseCode}";
                Debug.LogError($"[API] Request error: {response.Error}");
                
                // Token expired?
                if (request.responseCode == 401)
                {
                    Logout();
                }
            }

            if (GameSettings.Instance?.DebugMode == true)
            {
                Debug.Log($"[API] {request.method} {request.url} -> {request.responseCode}: {(response.Success ? "OK" : response.Error)}");
            }

            return response;
        }

        #endregion
    }
}
