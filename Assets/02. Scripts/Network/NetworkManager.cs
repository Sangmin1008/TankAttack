using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TankAttack.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TankAttack.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }
        [Header("Network Settings")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private int serverPort = 7777;

        [Header("UI Settings")]
        [SerializeField] private Button connectButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button exitButton;

        [Header("Player Settings")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private int localPlayerId = -1;

        private GameObject _playerPrefabInstance;
        private UdpGameClient _udpClient;

        public event Action<int, Vector3, Vector3> OnPlayerUpdated;
        public event Action<int, Vector3, Vector3> OnFired;
        
        public readonly Dictionary<int, GameObject> ConnectedPlayers = new Dictionary<int, GameObject>();
        
        #region 유니티 생명주기

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            connectButton.onClick.AddListener(OnConnectButtonClicked);
            joinButton.onClick.AddListener(OnJoinButtonClicked);
            exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        private void OnDisable()
        {
            connectButton.onClick.RemoveAllListeners();
            joinButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            _udpClient = new UdpGameClient();
            _udpClient.OnNetworkEvent += OnNetworkEventReceived;
            
            // 버튼 초기화 설정
            connectButton.interactable = true;
            joinButton.interactable = false;
            exitButton.interactable = false;
        }

        #endregion

        #region 네트워크 이벤트 처리

        private void OnNetworkEventReceived(NetworkEventData eventData)
        {
            switch (eventData.EventType)
            {
                case NetworkEventType.Connect:
                    Debug.Log("서버에 연결되었습니다.");
                    break;
                case NetworkEventType.Disconnect:
                    Debug.Log("서버 연결이 종료되었습니다.");
                    break;
                case NetworkEventType.DataReceive:
                    Debug.Log($"데이터 수신: {eventData.JsonData}");
                    HandleReceivedData(eventData.JsonData);
                    break;
                case NetworkEventType.Error:
                    Debug.LogError($"네트워크 오류: {eventData.JsonData}");
                    break;
            }
        }
        
        #endregion

        #region 메시지 전송 로직

        // 접속 요청 메시지 전송
        private async Task SendJoinRequestAsync()
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
        private async Task SendLeaveRequestAsync()
        {
            var leavePacket = new GamePacket
            {
                Type = (int)PacketType.PlayerLeave,
                PlayerId = localPlayerId,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            string jsonData = JsonUtility.ToJson(leavePacket);
            await _udpClient.SendDataAsync(jsonData);
            
            // 로컬 플레이어 정보 초기화 (재접속)
            localPlayerId = -1;
            _playerPrefabInstance = null;
        }
        
        // 이동 및 회전 데이터 전송
        public async Task SendPlayerUpdateAsync(Vector3 position, Vector3 rotation)
        {
            var updatePacket = new GamePacket
            {
                Type = (int)PacketType.PlayerUpdate,
                PlayerId = localPlayerId,
                Position = position,
                Rotation = rotation,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            string jsonData = JsonUtility.ToJson(updatePacket);
            await _udpClient.SendDataAsync(jsonData);
        }
        
        // 발사 메시지 전송
        public async Task SendFireAsync(int playerId, Vector3 position, Vector3 rotation)
        {
            var firePacket = new GamePacket
            {
                Type = (int)PacketType.PlayerFire,
                PlayerId = localPlayerId,
                Position = position,
                Rotation = rotation,
                LastUpdateTime = DateTime.UtcNow.ToString(),
            };
            string jsonData = JsonUtility.ToJson(firePacket);
            await _udpClient.SendDataAsync(jsonData);
        }
        
        #endregion

        #region 수신 메시지 처리 로직

        private void HandleReceivedData(string jsonData)
        {
            try
            {
                // JSON 역직렬화
                GamePacket packet = JsonUtility.FromJson<GamePacket>(jsonData);
                
                Vector3 position = JsonParser.ExtractVector3Value(jsonData, "Position");
                Vector3 rotation = JsonParser.ExtractVector3Value(jsonData, "Rotation");
                
                // 패킷 타입에 따라 분기
                switch ((PacketType)packet.Type)
                {
                    case PacketType.PlayerSpawn:
                        SpawnPlayer(packet, position, rotation);
                        break;
                    case PacketType.PlayerUpdate:
                        Debug.Log($"플레이어 업데이트 - ID: {packet.PlayerId}, 위치: {position}, 회전: {rotation}");
                        // 타 플레잉의 위치와 회전 업데이트 처리
                        OnPlayerUpdated?.Invoke(packet.PlayerId, position, rotation);
                        
                        break;
                    case PacketType.PlayerDespawn:
                        Debug.Log($"플레이어 디스폰 - ID: {packet.PlayerId}");
                        // 플레이어 제거 처리
                        if (ConnectedPlayers.TryGetValue(packet.PlayerId, out GameObject playerTank))
                        {
                            Destroy(playerTank);
                            ConnectedPlayers.Remove(packet.PlayerId);
                            Debug.Log($"플레이어 제거 완료: ID: {packet.PlayerId}");
                        }
                        break;
                    case PacketType.PlayerFire:
                        Debug.Log($"Fire: ID: {packet.PlayerId}");
                        // 발사 이벤트 발생
                        OnFired?.Invoke(packet.PlayerId, packet.Position, packet.Rotation);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"파싱 오류: {e.Message}");
            }
        }

        #endregion

        #region 플레이어 스폰 로직

        private void SpawnPlayer(GamePacket packet, Vector3 position, Vector3 rotation)
        {
            Debug.Log($"플레이어 스폰 - ID: {packet.PlayerId}, 위치: {position}, 회전: {rotation}");
            if (localPlayerId == -1)
            {
                localPlayerId = packet.PlayerId;
            }

            if (localPlayerId == packet.PlayerId) // 자신의 플레이어인 경우
            {
                if (_playerPrefabInstance == null)
                {
                    _playerPrefabInstance = Instantiate(playerPrefab, position, Quaternion.Euler(rotation));
                    
                    // 생성한 플레이어의 정보 설정
                    var ntv = _playerPrefabInstance.GetComponent<NetworkTransformView>();
                    ntv.PlauerId = packet.PlayerId;
                    ntv.IsMine = true;
                    
                    // 플레이어 목록에 추가
                    ConnectedPlayers[packet.PlayerId] = _playerPrefabInstance;
                    Debug.Log($"내 플레이어 스폰 완료 - ID : {packet.PlayerId}");
                }
            }
            else
            {
                var otherTank = Instantiate(playerPrefab, position, Quaternion.Euler(rotation));
                // 생성한 플레이어의 정보 설정
                var ntv = otherTank.GetComponent<NetworkTransformView>();
                ntv.PlauerId = packet.PlayerId;
                ntv.IsMine = false;
                ConnectedPlayers[packet.PlayerId] = otherTank;
                Debug.Log($"다른 플레이어 스폰 완료 - ID : {packet.PlayerId}");
            }
        }

        #endregion

        #region 버튼 이벤트 처리

        private async void OnConnectButtonClicked()
        {
            await _udpClient.ConnectServerAsync(serverIP, serverPort);
            connectButton.interactable = false;
            joinButton.interactable = true;
        }

        private async void OnJoinButtonClicked()
        {
            await SendJoinRequestAsync();
            joinButton.interactable = false;
            exitButton.interactable = true;
        }

        private async void OnExitButtonClicked()
        {
            // 접속 해지 요청 메시지 전송
            await SendLeaveRequestAsync();
            
            // 모든 다른 플레이어를 삭제
            foreach (var player in ConnectedPlayers.Values)
            {
                Destroy(player);
            }
            ConnectedPlayers.Clear();
            
            connectButton.interactable = true;
            exitButton.interactable = false;
        }

        #endregion
    }
}