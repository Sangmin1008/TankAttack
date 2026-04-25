using System;
using UnityEngine;

namespace TankAttack.Network
{
    public class NetworkTransformView : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        private Transform _transform;
        private Vector3 _prevPosition;

        public int PlauerId;
        public bool IsMine;

        private void Awake()
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            _transform = GetComponent<Transform>();
        }

        private void OnEnable()
        {
            networkManager.OnPlayerUpdated += HandlePlayerUpdated;
        }

        private void OnDisable()
        {
            networkManager.OnPlayerUpdated -= HandlePlayerUpdated;
        }

        private void HandlePlayerUpdated(int updatedPlayerId, Vector3 position, Vector3 rotation)
        {
            if (updatedPlayerId != PlauerId) return;
            if (IsMine) return;

            _transform.position = position;
            _transform.rotation = Quaternion.Euler(rotation);
        }

        private void Update()
        {
            if (IsMine && (_prevPosition - transform.position).sqrMagnitude > 0.001f)
            {
                _prevPosition = _transform.position;
                _ = networkManager.SendPlayerUpdateAsync(_transform.position, new Vector3(0, _transform.rotation.eulerAngles.y, 0));
            }
        }
    }
}