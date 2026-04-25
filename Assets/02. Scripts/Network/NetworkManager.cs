using System;
using System.Threading.Tasks;
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