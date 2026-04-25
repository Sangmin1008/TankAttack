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
    }
}