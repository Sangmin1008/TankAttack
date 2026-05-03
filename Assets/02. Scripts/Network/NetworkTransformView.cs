using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TankAttack.Network
{
    public class NetworkTransformView : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        private Transform _transform;
        
        public int PlauerId;
        public bool IsMine;

        [Header("Network Sync Settings")]
        [SerializeField] private float sendInterval = 0.1f;
        [SerializeField] private float lerpSpeed = 15f;

        private float _sendTimer;
        private Vector3 _prevPosition;

        // 타 플레이어의 목표 위치와 회전값 (보간용)
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        
        public event Action OnPositionReceived;

        private void Awake()
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            _transform = GetComponent<Transform>();
        }

        private void Start()
        {
            // 초기 목표 위치/회전값 세팅
            _targetPosition = _transform.position;
            _targetRotation = _transform.rotation;
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

            _targetPosition = position;
            _targetRotation = Quaternion.Euler(rotation);
            
            OnPositionReceived?.Invoke();
        }
        
        public void SnapToTarget()
        {
            _transform.position = _targetPosition;
            _transform.rotation = _targetRotation;
        }

        private void Update()
        {
            if (IsMine)
            {
                _sendTimer += Time.deltaTime;
                
                if (_sendTimer >= sendInterval)
                {
                    if ((_prevPosition - _transform.position).sqrMagnitude > 0.001f || 
                        _transform.hasChanged)
                    {
                        _prevPosition = _transform.position;
                        _transform.hasChanged = false;
                        
                        networkManager.SendPlayerUpdateAsync(_transform.position, new Vector3(0, _transform.rotation.eulerAngles.y, 0)).Forget();
                    }
                    _sendTimer = 0f;
                }
            }
            else
            {
                _transform.position = Vector3.Lerp(_transform.position, _targetPosition, Time.deltaTime * lerpSpeed);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, _targetRotation, Time.deltaTime * lerpSpeed);
            }
        }
    }
}