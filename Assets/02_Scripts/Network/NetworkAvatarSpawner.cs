using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace FireLink119.Network
{
    public class NetworkAvatarSpawner : NetworkRunnerCallbacksBehaviour
    {
        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedAvatars = new Dictionary<PlayerRef, NetworkObject>();

        private NetworkPrefabRef _avatarPrefab;
        private Vector3 _spawnOrigin;
        private float _spawnSpacing;

        public void Initialize(NetworkPrefabRef avatarPrefab, Vector3 spawnOrigin, float spawnSpacing)
        {
            _avatarPrefab = avatarPrefab;
            _spawnOrigin = spawnOrigin;
            _spawnSpacing = spawnSpacing;
        }

        public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || _spawnedAvatars.ContainsKey(player))
            {
                return;
            }

            if (!_avatarPrefab.IsValid)
            {
                Debug.LogWarning("[NetworkAvatarSpawner] Network avatar prefab is not assigned.");
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition();
            NetworkObject avatarObject = runner.Spawn(
                prefabRef: _avatarPrefab,
                position: spawnPosition,
                rotation: Quaternion.identity,
                inputAuthority: player);

            _spawnedAvatars[player] = avatarObject;
            runner.SetPlayerObject(player, avatarObject);
        }

        public override void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!_spawnedAvatars.TryGetValue(player, out NetworkObject avatarObject))
            {
                return;
            }

            if (avatarObject != null)
            {
                runner.Despawn(avatarObject);
            }

            _spawnedAvatars.Remove(player);
        }

        public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _spawnedAvatars.Clear();
        }

        private Vector3 GetSpawnPosition()
        {
            int spawnIndex = _spawnedAvatars.Count;
            return _spawnOrigin + Vector3.right * (_spawnSpacing * spawnIndex);
        }
    }
}
