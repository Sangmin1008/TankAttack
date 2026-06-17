using System;
using System.Numerics;
using MemoryPack;

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
    public enum PacketType : byte
    {
        PlayerJoin = 1,
        PlayerLeave = 2,
        PlayerUpdate = 3,
        PlayerSpawn = 4,
        PlayerDespawn = 5,
        PlayerFire = 6,
        Heartbeat = 7,
        Timeout = 8,
        PlayerHit = 9,
        ItemSpawn = 10,
        ItemPickup = 11,
        ItemConsumed = 12,
        PlayerEmoticon = 13,
		JoinSuccess = 14,
        
        Ack = 99
    }
    // 네트워크 이벤트 데이터 저장 클래스
    public class NetworkEventData
    {
        public NetworkEventType EventType { get; set; }
        public byte[] RawData { get; set; }
        public int DataLength { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    // 송수신 데이터 패킷
    [MemoryPackable]
    public partial class GamePacket
    {
        public PacketType Type;
        public uint Sequence;
        public bool IsReliable;
        public int PlayerId;
        public Vector3 Position;
        public Vector3 Rotation;
        public int TargetId;
        public int Damage;
        public int ItemId;
        public int ItemType;
        public int EmoticonId;
        public DateTime Timestamp;
        
        [MemoryPackConstructor]
        public GamePacket()
        {
            Position = Vector3.Zero;
            Rotation = Vector3.Zero;
            Timestamp = DateTime.UtcNow;
        }
    }
}