using System;
using Cysharp.Threading.Tasks;
using R3;
using TankAttack.Network.Manager;
using UnityEngine;
using VContainer;

namespace TankAttack.Network
{
    public class NetworkTransformView : MonoBehaviour
    {
        [Header("Network Sync Settings")]
        [SerializeField] private float sendInterval = 0.1f;
        [SerializeField] private float lerpSpeed = 15f;
        
        public int PlayerId;
        public bool IsMine;

        private Transform _transform;
        private float _sendTimer;
        private Vector3 _prevPosition;

        // 타 플레이어의 목표 위치와 회전값 (보간용)
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        
        private readonly Subject<Unit> _onPositionReceived = new();
        public Observable<Unit> OnPositionReceived => _onPositionReceived;

        // DI로 주입받을 객체들
        private NetworkModel _model;
        private NetworkPresenter _presenter;
        private readonly CompositeDisposable _disposables = new();
        
        [Inject]
        public void Construct(NetworkModel model, NetworkPresenter presenter)
        {
            _model = model;
            _presenter = presenter;
        }
        
        private void Awake()
        {
            _transform = GetComponent<Transform>();
        }

        private void Start()
        {
            // 초기 목표 위치/회전값 세팅
            _targetPosition = _transform.position;
            _targetRotation = _transform.rotation;
            
            if (!IsMine)
            {
                _model.OnPlayerUpdated
                    // 내 ID와 일치하는 패킷만 통과시킵니다. (if문 대체)
                    .Where(packet => packet.playerId == PlayerId) 
                    .Subscribe(packet =>
                    {
                        _targetPosition = packet.pos;
                        _targetRotation = Quaternion.Euler(packet.rot);
                        
                        // 이벤트 발생 알림
                        _onPositionReceived.OnNext(Unit.Default);
                    })
                    .AddTo(_disposables); // 파괴될 때 자동 해제
            }
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
                        
                        _presenter.SendPlayerUpdateAsync(_transform.position, new Vector3(0, _transform.rotation.eulerAngles.y, 0)).Forget();
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
        
        private void OnDestroy()
        {
            _disposables.Dispose();
            _onPositionReceived.Dispose();
        }
    }
}