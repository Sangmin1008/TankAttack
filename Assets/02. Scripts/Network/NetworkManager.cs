using System;
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
                        Debug.Log($"플레이어 스폰 - ID: {packet.PlayerId}, 위치: {position}, 회전: {rotation}");
                        // 플레이어 id 설정
                        if (localPlayerId == -1)
                        {
                            localPlayerId = packet.PlayerId;
                        }
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

        #region 버튼 이벤트 처리

        private async void OnConnectButtonClicked()
        {
            await _udpClient.ConnectServerAsync(serverIP, serverPort);
        }

        private async void OnJoinButtonClicked()
        {
            await SendJoinRequestAsync();
        }

        private void OnExitButtonClicked()
        {
            
        }

        #endregion
    }
}