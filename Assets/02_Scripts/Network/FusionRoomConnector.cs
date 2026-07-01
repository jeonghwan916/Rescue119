using System.IO;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireLink119.Network
{
    public class FusionRoomConnector : MonoBehaviour
    {
        [SerializeField] private string _roomSceneName = "RoomScene";
        [SerializeField] private int _maxPlayers = 2;
        [SerializeField] private NetworkPrefabRef _playerAvatarPrefab;
        [SerializeField] private Vector3 _playerSpawnOrigin = Vector3.zero;
        [SerializeField] private float _playerSpawnSpacing = 1.5f;

        private bool _isStarting;
        private NetworkRunner _runner;

        public async void StartRoom(LobbyRoomRole role, string roomCode)
        {
            // Fusion 방 생성/입장은 비동기라서, 같은 입력이 연타되어 Runner가 중복 생성되는 것을 막는다.
            if (_isStarting)
            {
                return;
            }

            if (!IsValidRoomCode(roomCode))
            {
                Debug.LogWarning("[FusionRoomConnector] Room code must be exactly 4 digits.");
                return;
            }

            int roomSceneBuildIndex = GetBuildIndexBySceneName(_roomSceneName);
            if (roomSceneBuildIndex < 0)
            {
                Debug.LogError($"[FusionRoomConnector] Room scene is not registered in Build Settings: {_roomSceneName}");
                return;
            }

            GameMode gameMode = role == LobbyRoomRole.Host ? GameMode.Host : GameMode.Client;
            _isStarting = true;

            GameObject runnerObject = new GameObject($"Photon Fusion Runner ({gameMode})");
            DontDestroyOnLoad(runnerObject);

            _runner = runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            runnerObject.AddComponent<NetworkAvatarInputProvider>();
            NetworkAvatarSpawner avatarSpawner = runnerObject.AddComponent<NetworkAvatarSpawner>();
            avatarSpawner.Initialize(_playerAvatarPrefab, _playerSpawnOrigin, _playerSpawnSpacing);

            NetworkSceneManagerDefault sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

            // OneShot 예제와 같은 StartGame 진입 구조를 쓰고, 로비에서 입력한 4자리 코드를 SessionName으로 사용한다.
            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = roomCode,
                Scene = SceneRef.FromIndex(roomSceneBuildIndex),
                SceneManager = sceneManager,
                PlayerCount = _maxPlayers,
                IsOpen = true,
                IsVisible = true,
                EnableClientSessionCreation = false
            });

            if (!result.Ok)
            {
                Debug.LogWarning($"[FusionRoomConnector] Photon room start failed. Mode: {gameMode}, Code: {roomCode}, Reason: {result.ShutdownReason}");
                Destroy(runnerObject);
                _runner = null;
                _isStarting = false;
                return;
            }

            Debug.Log($"[FusionRoomConnector] Photon room started. Mode: {gameMode}, Code: {roomCode}");
        }

        private bool IsValidRoomCode(string roomCode)
        {
            // Photon SessionName으로 넘기기 전에 로비 규칙을 한 번 더 검증해서 잘못된 방 생성을 막는다.
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 4)
            {
                return false;
            }

            for (int i = 0; i < roomCode.Length; i++)
            {
                if (!char.IsDigit(roomCode[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetBuildIndexBySceneName(string sceneName)
        {
            // Fusion scene loading에는 SceneRef가 필요하지만, Inspector에서는 씬 이름이 관리하기 쉬워서 실행 시 변환한다.
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string buildSceneName = Path.GetFileNameWithoutExtension(scenePath);

                if (buildSceneName == sceneName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
