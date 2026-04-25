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
        
        public readonly Dictionary<int, GameObject> ConnectedPlayers = new Dictionary<int, GameObject>();
        
        public bool IsMine => localPlayerId != -1;

        #region 유니티 생명주기

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
                        // TODO 타 플레잉의 위치와 회전 업데이트 처리
                        if (!IsMine)
                        {
                            // TODO 업데이트 처리
                        }
                        break;
                    case PacketType.PlayerDespawn:
                        Debug.Log($"플레이어 디스폰 - ID: {packet.PlayerId}");
                        // TODO 플레이어 제거 처리
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

        private void OnExitButtonClicked()
        {
            connectButton.interactable = true;
            exitButton.interactable = false;
        }

        #endregion
    }
}