using System;

namespace TankAttack.Network
{
    // 네트워크 이벤트 타입
    public enum NetworkEventType
    {
        Connect,
        Disconnect,
        DataReceive,
        Error
    }
    
    // 패킷 타입
    public enum PacketType
    {
        PlayerJoined = 1,
        PlayerLeave = 2,
        PlayerUpdate = 3,
        PlayerSpawn = 4,
        PlayerDespawn = 5,
        PlayerFire = 6,
    }
    // 네트워크 이벤트 데이터 저장 클래스
    public class NetworkEventData
    {
        public NetworkEventType EventType { get; set; }
        public string JsonData { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    // 송수신 데이터 패킷
    [Serializable]
    public class GamePacket
    {
        public int Type;
        public int PlayerId;
        public UnityEngine.Vector3 Position;
        public UnityEngine.Vector3 Rotation;
        public string LastUpdateTime;
    }
}