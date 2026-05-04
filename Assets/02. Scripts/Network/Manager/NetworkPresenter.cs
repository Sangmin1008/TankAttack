using System;
using Cysharp.Threading.Tasks;
using R3;
using TankAttack.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TankAttack.Network.Manager
{
    public class NetworkPresenter : IInitializable, IDisposable
    {
        private readonly NetworkModel _model;
        private readonly NetworkUIView _view;
        private readonly UdpGameClient _udpClient;
        private readonly IObjectResolver _resolver;
        
        private readonly CompositeDisposable _disposables = new();
        
        [Inject]
        public NetworkPresenter(NetworkModel model, NetworkUIView view, UdpGameClient udpClient, IObjectResolver resolver)
        {
            _model = model;
            _view = view;
            _udpClient = udpClient;
            _resolver = resolver;
        }
        
        public void Initialize()
        {
            BindUI();
            BindNetworkEvents();
            StartHeartbeatRoutine();
        }
        
        private void BindUI()
        {
            Observable.CombineLatest(_model.IsConnected, _model.IsJoined, (isConnected, isJoined) => (isConnected, isJoined))
                .Subscribe(state =>
                {
                    _view.SetButtonStates(
                        canConnect: !state.isConnected,
                        canJoin: state.isConnected && !state.isJoined,
                        canExit: state.isJoined
                    );
                })
                .AddTo(_disposables);

            _view.OnConnectClicked.SubscribeAwait(async (_, _) =>
            {
                await _udpClient.ConnectServerAsync(_view.serverIP, _view.serverPort);
                await UniTask.SwitchToMainThread();
            }).AddTo(_disposables);

            _view.OnJoinClicked.SubscribeAwait(async (_, _) =>
            {
                await SendJoinRequestAsync();
            }).AddTo(_disposables);

            _view.OnExitClicked.SubscribeAwait(async (_, _) =>
            {
                await SendLeaveRequestAsync();
                await UniTask.SwitchToMainThread();
                ClearAllPlayers();
            }).AddTo(_disposables);
        }

        private void BindNetworkEvents()
        {
            _udpClient.OnNetworkEvent += HandleNetworkEvent;
        }

        private void HandleNetworkEvent(NetworkEventData eventData)
        {
            switch (eventData.EventType)
            {
                case NetworkEventType.Connect:
                    _model.IsConnected.Value = true;
                    Debug.Log("서버에 연결되었습니다.");
                    break;
                case NetworkEventType.Disconnect:
                    _model.IsConnected.Value = false;
                    _model.IsJoined.Value = false;
                    ClearAllPlayers();
                    break;
                case NetworkEventType.DataReceive:
                    ParseAndHandlePacket(eventData.JsonData);
                    break;
                case NetworkEventType.Error:
                    Debug.LogError($"네트워크 오류: {eventData.ErrorMessage}");
                    break;
            }
        }

        private void StartHeartbeatRoutine()
        {
            Observable.Interval(TimeSpan.FromSeconds(_view.heartbeatInterval))
                .Where(_ => _model.IsJoined.Value)
                .SubscribeAwait(async (_, _) => 
                {
                    await SendHeartBeatAsync(_model.LocalPlayerId.Value);
                })
                .AddTo(_disposables);
        }

        #region 수신 패킷 처리
        private void ParseAndHandlePacket(string jsonData)
        {
            try
            {
                GamePacket packet = JsonUtility.FromJson<GamePacket>(jsonData);
                switch ((PacketType)packet.Type)
                {
                    case PacketType.PlayerSpawn:
                        Vector3 pos = JsonParser.ExtractVector3Value(jsonData, "Position");
                        Vector3 rot = JsonParser.ExtractVector3Value(jsonData, "Rotation");
                        SpawnPlayer(packet, pos, rot);
                        break;
                    case PacketType.PlayerUpdate:
                        Vector3 uPos = JsonParser.ExtractVector3Value(jsonData, "Position");
                        Vector3 uRot = JsonParser.ExtractVector3Value(jsonData, "Rotation");
                        _model.OnPlayerUpdated.OnNext((packet.PlayerId, uPos, uRot)); // Subject 발행
                        break;
                    case PacketType.PlayerDespawn:
                        Debug.Log($"플레이어 디스폰 - ID: {packet.PlayerId}");
                        // 플레이어 제거 처리
                        if (_model.ConnectedPlayers.TryGetValue(packet.PlayerId, out GameObject playerTank))
                        {
                            Object.Destroy(playerTank);
                            _model.ConnectedPlayers.Remove(packet.PlayerId);
                            Debug.Log($"플레이어 제거 완료: ID: {packet.PlayerId}");
                        }
                        break;
                    case PacketType.PlayerFire:
                        _model.OnFired.OnNext((packet.PlayerId, packet.Position, packet.Rotation));
                        break;
                    case PacketType.Timeout:
                        Debug.Log("서버에서 타임아웃 패킷 수신");
                        _model.LocalPlayerId.Value = -1;
                        _view.playerPrefab = null;
                        foreach (var other in _model.ConnectedPlayers.Values)
                        {
                            Object.Destroy(other);
                        }

                        if (_model.ConnectedPlayers.TryGetValue(packet.PlayerId, out var timeoutPlayer))
                        {
                            Object.Destroy(timeoutPlayer);
                        }
                        
                        _model.ConnectedPlayers.Clear();
                        break;
                    
                }
            }
            catch (Exception e) { Debug.LogError($"파싱 오류: {e.Message}"); }
        }

        private void SpawnPlayer(GamePacket packet, Vector3 position, Vector3 rotation)
        {
            if (_model.LocalPlayerId.Value == -1)
            {
                _model.LocalPlayerId.Value = packet.PlayerId;
                _model.IsJoined.Value = true;
                position = new Vector3(UnityEngine.Random.Range(-20f, 20f), 0, UnityEngine.Random.Range(-20f, 20f));
            }

            // 생성된 오브젝트(NetworkTransformView) 내부의 [Inject] 어노테이션을 찾아 자동으로 의존성을 주입
            var playerObj = _resolver.Instantiate(_view.playerPrefab, position, Quaternion.Euler(rotation));
            var ntv = playerObj.GetComponent<NetworkTransformView>();
            
            ntv.PlayerId = packet.PlayerId;
            ntv.IsMine = (_model.LocalPlayerId.Value == packet.PlayerId);

            _model.ConnectedPlayers[packet.PlayerId] = playerObj;
        }

        private void ClearAllPlayers()
        {
            foreach (var player in _model.ConnectedPlayers.Values) { Object.Destroy(player); }
            _model.ConnectedPlayers.Clear();
            _model.LocalPlayerId.Value = -1;
            _model.IsJoined.Value = false;
        }
        #endregion

        #region 메시지 전송 로직

        // 접속 요청 메시지 전송
        private async UniTask SendJoinRequestAsync()
        {
            var connectPacket = new GamePacket
            {
                Type = (int)PacketType.PlayerJoined,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            
            string jsonData = JsonUtility.ToJson(connectPacket);
            await _udpClient.SendDataAsync(jsonData);
        }
        
        // 접속 해지 요청 메시지 전송
        private async UniTask SendLeaveRequestAsync()
        {
            var leavePacket = new GamePacket
            {
                Type = (int)PacketType.PlayerLeave,
                PlayerId = _model.LocalPlayerId.Value,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            string jsonData = JsonUtility.ToJson(leavePacket);
            await _udpClient.SendDataAsync(jsonData);
            
            // 로컬 플레이어 정보 초기화 (재접속)
            _model.LocalPlayerId.Value = -1;
            _view.playerPrefab = null;
        }
        
        // 이동 및 회전 데이터 전송
        public async UniTask SendPlayerUpdateAsync(Vector3 position, Vector3 rotation)
        {
            var updatePacket = new GamePacket
            {
                Type = (int)PacketType.PlayerUpdate,
                PlayerId = _model.LocalPlayerId.Value,
                Position = position,
                Rotation = rotation,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            string jsonData = JsonUtility.ToJson(updatePacket);
            await _udpClient.SendDataAsync(jsonData);
        }
        
        // 발사 메시지 전송
        public async UniTask SendFireAsync(int playerId, Vector3 position, Vector3 rotation)
        {
            var firePacket = new GamePacket
            {
                Type = (int)PacketType.PlayerFire,
                PlayerId = playerId,
                Position = position,
                Rotation = rotation,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            string jsonData = JsonUtility.ToJson(firePacket);
            await _udpClient.SendDataAsync(jsonData);
        }

        public async UniTask SendHeartBeatAsync(int playerId)
        {
            var heartbeatPacket = new GamePacket
            {
                Type = (int)PacketType.Heartbeat,
                PlayerId = playerId,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            
            string jsonData = JsonUtility.ToJson(heartbeatPacket);
            await _udpClient.SendDataAsync(jsonData);
        }
        
        #endregion

        public void Dispose()
        {
            _udpClient.OnNetworkEvent -= HandleNetworkEvent;
            _disposables.Dispose();
        }
    }
}