using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TankAttack.Network
{
    public class UdpGameClient : IDisposable
    {
        private UdpClient _udpClient;
        private IPEndPoint _serverEP;
        
        private CancellationTokenSource _cts;

        private bool _isConnected;
        public bool IsConnected => _isConnected;
        
        public event Action<NetworkEventData> OnNetworkEvent;

        public async Task ConnectServerAsync(string ip, int port)
        {
            try
            {
                if (_isConnected)
                {
                    Debug.Log("이미 연결되어 있습니다.");
                    return;
                }
                
                _serverEP = new IPEndPoint(IPAddress.Parse(ip), port);
                _udpClient = new UdpClient();
                _udpClient.Connect(_serverEP);
                
                _cts = new CancellationTokenSource();
                _isConnected = true;
                
                // 수신 루프 시작
                _ = Task.Run(ReceiveLoopAsync);
                
                // 연결 성공 이벤트 발생
                DispatchEvent(new NetworkEventData
                {
                    EventType = NetworkEventType.Connect,
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"연결 실패: {e.Message}");
                
                // 네트워크 이벤트 발생
                DispatchEvent(new NetworkEventData
                {
                    EventType = NetworkEventType.Error,
                    ErrorMessage = e.Message,
                });
            }   
        }

        #region 메시지 수신 루프

        private async Task ReceiveLoopAsync()
        {
            Debug.Log("UDP 수신 루프 시작");
            try
            {
                while (_isConnected && !_cts.IsCancellationRequested)
                {
                    // 비동기 이벤트 수신
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    
                    string jsonData = Encoding.UTF8.GetString(result.Buffer);
                    
                    // 이벤트 발생
                    DispatchEvent(new NetworkEventData
                    {
                        EventType = NetworkEventType.DataReceive,
                        JsonData = jsonData
                    });
                }
            }
            catch (Exception e)
            {
                if (_isConnected)
                {
                    Debug.Log($"UDP 수신 오류: {e.Message}");
                    DispatchEvent(new NetworkEventData
                    {
                        EventType = NetworkEventType.Error,
                        ErrorMessage = $"UDP 수신 오류: {e.Message}",
                    });
                }
            }
        }

        #endregion
        
        // 전송 메서드
        public async Task SendDataAsync(string jsonData)
        {
            if (!_isConnected)
            {
                Debug.Log("서버에 연결되어 있지 않습니다.");
                return;
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(jsonData);
                await _udpClient.SendAsync(data, data.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"데이터 전송 오류: {e.Message}");
                DispatchEvent(new NetworkEventData
                {
                    EventType = NetworkEventType.Error,
                    ErrorMessage = e.Message,
                });
            }
        }

        // 메인 스레드로 이벤트 전달을 위해 큐에 추가
        private void DispatchEvent(NetworkEventData eventData)
        {
            MainThreadDispatcher.Instance.Enqueue(() => { OnNetworkEvent?.Invoke(eventData); });
        }
        
        public void Dispose()
        {
            if (!_isConnected) return;
            
            _isConnected = false;
            _cts?.Cancel();
            _udpClient?.Dispose();
            
            DispatchEvent(new NetworkEventData
            {
                EventType = NetworkEventType.Disconnect,
            });
        }
    }
}