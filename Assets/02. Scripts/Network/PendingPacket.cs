using System;
using TankAttack.Network;

public class PendingPacket
{
    public GamePacket Packet;
    public DateTime LastSentTime;
    public int RetryCount;
}