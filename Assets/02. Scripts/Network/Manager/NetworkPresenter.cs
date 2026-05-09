using System;
using System.Collections.Concurrent;
using System.Threading;
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
        private long _nextSequence = 0;
        private readonly ConcurrentDictionary<uint, PendingPacket> _pendingPackets = new();
        private readonly ConcurrentDictionary<uint, byte> _processedServerReliableSequences = new();
        private CancellationTokenSource _watchdogCts;
        
        private readonly NetworkModel _model;
        private readonly NetworkUIView _view;
        private readonly UdpGameClient _udpClient;
        private readonly IObjectResolver _resolver;
        private readonly HpBarManager _hpBarManager;
        
        private readonly CompositeDisposable _disposables = new();
        
        [Inject]
        public NetworkPresenter(NetworkModel model, NetworkUIView view, UdpGameClient udpClient, IObjectResolver resolver, HpBarManager hpBarManager)
        {
            _model = model;
            _view = view;
            _udpClient = udpClient;
            _resolver = resolver;
            _hpBarManager = hpBarManager;
        }
        
        public void Initialize()
        {
            BindUI();
            BindNetworkEvents();
            StartHeartbeatRoutine();
            StartAckRoutine();
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
                _hpBarManager.ClearAll();
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
                    _hpBarManager.ClearAll();
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

        private void StartAckRoutine()
        {
            _watchdogCts = new CancellationTokenSource();
            StartWatchdogAsync(_watchdogCts.Token).Forget();
        }
        
        private async UniTask StartWatchdogAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(100, cancellationToken: token);

                    DateTime now = DateTime.UtcNow;

                    foreach (var kvp in _pendingPackets)
                    {
                        var pending = kvp.Value;

                        if ((now - pending.LastSentTime).TotalMilliseconds > 200)
                        {
                            if (pending.RetryCount < 5)
                            {
                                pending.RetryCount++;
                                pending.LastSentTime = now;

                                string jsonData = JsonUtility.ToJson(pending.Packet);
                                _udpClient.SendDataAsync(jsonData).Forget();
                                Debug.Log($"클라이언트 -> 서버 패킷 {kvp.Key} 재전송 (시도 {pending.RetryCount})");
                            }
                            else
                            {
                                // 5번 시도 실패
                                Debug.LogError($"패킷 {kvp.Key} 최종 전송 실패. 서버 응답 없음.");
                                _pendingPackets.TryRemove(kvp.Key, out _);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                
            }
        }

        #region 수신 패킷 처리
        private void ParseAndHandlePacket(string jsonData)
        {
            try
            {
                GamePacket packet = JsonUtility.FromJson<GamePacket>(jsonData);
                if ((PacketType)packet.Type == PacketType.Ack)
                {
                    if (_pendingPackets.TryRemove(packet.Sequence, out _))
                    {
                        Debug.Log($"서버로부터 {packet.Sequence}번 패킷 수신 확인 받음.");
                    }
                    return;
                }

                if (packet.IsReliable)
                {
                    if (!_processedServerReliableSequences.TryAdd(packet.Sequence, 0))
                    {
                        SendAckAsync(packet.Sequence).Forget();
                        return;
                    }
                    SendAckAsync(packet.Sequence).Forget();
                }
                
                switch ((PacketType)packet.Type)
                {
                    case PacketType.JoinSuccess:
                        if (_model.LocalPlayerId.Value == -1)
                        {
                            _model.LocalPlayerId.Value = packet.PlayerId;
                            _model.IsJoined.Value = true;
                            Debug.Log($"[네트워크] 서버 접속 성공. 내 ID는: {packet.PlayerId}");
                        }
                        break;
                    case PacketType.PlayerSpawn:
                        Vector3 pos = JsonParser.ExtractVector3Value(jsonData, "Position");
                        Vector3 rot = JsonParser.ExtractVector3Value(jsonData, "Rotation");
                        SpawnPlayer(packet, pos, rot);
                        break;
                    case PacketType.PlayerUpdate:
                        Vector3 uPos = JsonParser.ExtractVector3Value(jsonData, "Position");
                        Vector3 uRot = JsonParser.ExtractVector3Value(jsonData, "Rotation");
                        _model.OnPlayerUpdated.OnNext((packet.PlayerId, uPos, uRot));
                        break;
                    case PacketType.PlayerDespawn:
                        Debug.Log($"플레이어 디스폰 - ID: {packet.PlayerId}");
                        // 플레이어 제거 처리
                        if (_model.ConnectedPlayers.TryGetValue(packet.PlayerId, out GameObject playerTank))
                        {
                            _hpBarManager.UnregisterHpBar(playerTank.transform);
                            Object.Destroy(playerTank);
                            _model.ConnectedPlayers.Remove(packet.PlayerId);
                            Debug.Log($"플레이어 제거 완료: ID: {packet.PlayerId}");
                        }
                        break;
                    case PacketType.PlayerFire:
                        _model.OnFired.OnNext((packet.PlayerId, packet.Position, packet.Rotation));
                        break;
                    case PacketType.PlayerHit:
                        _model.OnPlayerHit.OnNext((packet.TargetId, packet.Damage));
                        break;
                    case PacketType.Timeout:
                        Debug.Log("서버에서 타임아웃 패킷 수신");
                        _model.LocalPlayerId.Value = -1;
                        _hpBarManager.ClearAll();
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
                    case PacketType.ItemSpawn:
                        Vector3 itemPos = JsonParser.ExtractVector3Value(jsonData, "Position");
                        _model.OnItemSpawned.OnNext((packet.ItemId, packet.ItemType, itemPos));
                        break;
                    case PacketType.ItemConsumed:
                        _model.OnItemConsumed.OnNext((packet.ItemId, packet.PlayerId, packet.ItemType));
                        break;
                    case PacketType.PlayerEmoticon:
                        _model.OnEmoticonUsed.OnNext((packet.PlayerId, packet.EmoticonId));
                        break;
                    
                }
            }
            catch (Exception e) { Debug.LogError($"파싱 오류: {e.Message}"); }
        }

        private void SpawnPlayer(GamePacket packet, Vector3 position, Vector3 rotation)
        {
            if (_model.ConnectedPlayers.ContainsKey(packet.PlayerId))
                return;
            
            // if (_model.LocalPlayerId.Value == -1)
            // {
            //     _model.LocalPlayerId.Value = packet.PlayerId;
            //     _model.IsJoined.Value = true;
            //     position = new Vector3(UnityEngine.Random.Range(-20f, 20f), 0, UnityEngine.Random.Range(-20f, 20f));
            // }

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
            _processedServerReliableSequences.Clear();
        }
        #endregion

        #region 메시지 전송 로직
        // ACK 메시지 전송
        private async UniTask SendAckAsync(uint receivedSequence)
        {
            var ackPacket = new GamePacket
            {
                Type = (int)PacketType.Ack,
                Sequence = receivedSequence,
                LastUpdateTime = DateTime.UtcNow.ToString()
            };
        
            string jsonData = JsonUtility.ToJson(ackPacket);
            await _udpClient.SendDataAsync(jsonData);
        }
        
        private async UniTask SendReliableAsync(GamePacket packet)
        {
            // 시퀀스 번호 부여 및 Reliable 마킹
            uint seq = (uint)Interlocked.Increment(ref _nextSequence);
            packet.Sequence = seq;
            packet.IsReliable = true;

            // 장부에 기록
            _pendingPackets[seq] = new PendingPacket
            {
                Packet = packet,
                LastSentTime = DateTime.UtcNow,
                RetryCount = 0
            };

            // 발송
            string jsonData = JsonUtility.ToJson(packet);
            await _udpClient.SendDataAsync(jsonData);
        }

        // 접속 요청 메시지 전송
        private async UniTask SendJoinRequestAsync()
        {
            var connectPacket = new GamePacket
            {
                Type = (int)PacketType.PlayerJoined,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            
            // string jsonData = JsonUtility.ToJson(connectPacket);
            // await _udpClient.SendDataAsync(jsonData);
            await SendReliableAsync(connectPacket);
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
            // string jsonData = JsonUtility.ToJson(leavePacket);
            // await _udpClient.SendDataAsync(jsonData);
            await SendReliableAsync(leavePacket);
            
            // 로컬 플레이어 정보 초기화 (재접속)
            _model.LocalPlayerId.Value = -1;
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
            // string jsonData = JsonUtility.ToJson(firePacket);
            // await _udpClient.SendDataAsync(jsonData);
            
            await SendReliableAsync(firePacket);
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
        
        public async UniTask SendPlayerHitAsync(int targetId, int damage)
        {
            var hitPacket = new GamePacket
            {
                Type = (int)PacketType.PlayerHit,
                PlayerId = _model.LocalPlayerId.Value,
                TargetId = targetId,
                Damage = damage,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            // string jsonData = JsonUtility.ToJson(hitPacket);
            // await _udpClient.SendDataAsync(jsonData);
            
            await SendReliableAsync(hitPacket);
        }
        
        public async UniTask SendItemPickupAsync(int itemId)
        {
            var pickupPacket = new GamePacket
            {
                Type = (int)PacketType.ItemPickup,
                PlayerId = _model.LocalPlayerId.Value,
                ItemId = itemId,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            // string jsonData = JsonUtility.ToJson(pickupPacket);
            // await _udpClient.SendDataAsync(jsonData);
            
            await SendReliableAsync(pickupPacket);
        }
        
        public async UniTask SendEmoticonAsync(int emoticonId)
        {
            var emoticonPacket = new GamePacket
            {
                Type = (int)PacketType.PlayerEmoticon,
                PlayerId = _model.LocalPlayerId.Value,
                EmoticonId = emoticonId,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            // string jsonData = JsonUtility.ToJson(emoticonPacket);
            // await _udpClient.SendDataAsync(jsonData);
            await SendReliableAsync(emoticonPacket);
        }
        
        #endregion

        public void Dispose()
        {
            _udpClient.OnNetworkEvent -= HandleNetworkEvent;
            _watchdogCts?.Cancel();
            _disposables.Dispose();
        }
    }
}