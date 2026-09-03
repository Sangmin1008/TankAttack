using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        public async UniTask ConnectServerAsync(string ip, int port)
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
                // _ = Task.Run(ReceiveLoopAsync);
                ReceiveLoopAsync(_cts.Token).Forget();
                
                // 연결 성공 이벤트 발생
                await UniTask.SwitchToMainThread();
                DispatchEvent(new NetworkEventData
                {
                    EventType = NetworkEventType.Connect,
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"연결 실패: {e.Message}");
                
                // 네트워크 이벤트 발생
                await UniTask.SwitchToMainThread();
                DispatchEvent(new NetworkEventData
                {
                    EventType = NetworkEventType.Error,
                    ErrorMessage = e.Message,
                });
            }
        }

        #region 메시지 수신 루프

        private async UniTask ReceiveLoopAsync(CancellationToken token)
        {
            Debug.Log("UDP 수신 루프 시작");
            // 수신 대기 작업은 스레드 풀(백그라운드 스레드)에서 실행하도록 명시적 전환
            await UniTask.SwitchToThreadPool();
            
            try
            {
                while (_isConnected && !token.IsCancellationRequested)
                {
                    // 비동기 이벤트 수신
                    UdpReceiveResult result = await _udpClient.ReceiveAsync().ConfigureAwait(false);
                    
                    // string jsonData = Encoding.UTF8.GetString(result.Buffer);
                    //
                    // await UniTask.SwitchToMainThread(token);
                    //
                    // // 이벤트 발생
                    // DispatchEvent(new NetworkEventData
                    // {
                    //     EventType = NetworkEventType.DataReceive,
                    //     JsonData = jsonData
                    // });
                    //
                    // await UniTask.SwitchToThreadPool();
                    
                    DispatchEvent(new NetworkEventData
                    {
                        EventType = NetworkEventType.DataReceive,
                        RawData = result.Buffer,
                        DataLength = result.Buffer.Length
                    });
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException e)
            {
                DispatchEvent(new NetworkEventData { EventType = NetworkEventType.Error, ErrorMessage = e.Message });
            }
            catch (Exception e)
            {
                // UniTask 디스패처를 이용한 메인 스레드 복귀
                await UniTask.SwitchToMainThread();
                DispatchEvent(new NetworkEventData { EventType = NetworkEventType.Error, ErrorMessage = e.Message });
            }
        }

        #endregion
        
        // 전송 메서드
        public async UniTask SendDataAsync(byte[] binaryData)
        {
            if (!_isConnected)
            {
                Debug.Log("서버에 연결되어 있지 않습니다.");
                return;
            }
            
            if (binaryData == null || binaryData.Length == 0)
                return;

            try
            {
                await _udpClient.SendAsync(binaryData, binaryData.Length).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                DispatchEvent(new NetworkEventData
                {
                    EventType = NetworkEventType.Error, 
                    ErrorMessage = $"발송 오류: {e.Message}"
                });
            }
        }
        
        public void SendDataFast(ReadOnlySpan<byte> dataSpan)
        {
            if (!_isConnected || dataSpan.Length == 0) return;
            try
            {
                _udpClient.Client.Send(dataSpan, SocketFlags.None);
            }
            catch (Exception e)
            {
                DispatchEvent(new NetworkEventData { EventType = NetworkEventType.Error, ErrorMessage = $"빠른 발송 오류: {e.Message}" });
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;
            
            _isConnected = false;
            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient = new UdpClient();
            
            DispatchEvent(new NetworkEventData { EventType = NetworkEventType.Disconnect });
        }

        private void DispatchEvent(NetworkEventData eventData)
        {
            UniTask.Post(() => OnNetworkEvent?.Invoke(eventData));
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
        }
    }
}