using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace TankAttack.Network.Manager
{
    public class NetworkModel : IDisposable
    {
        public ReactiveProperty<bool> IsConnected { get; } = new(false);
        public ReactiveProperty<bool> IsJoined { get; } = new(false);
        public ReactiveProperty<int> LocalPlayerId { get; } = new(-1);
        
        public Dictionary<int, GameObject> ConnectedPlayers { get; } = new();

        public Subject<(int playerId, Vector3 pos, Vector3 rot)> OnPlayerUpdated { get; } = new();
        public Subject<(int playerId, Vector3 pos, Vector3 rot)> OnFired { get; } = new();
        public Subject<(int targetId, int damage)> OnPlayerHit { get; } = new();
        public Subject<(int itemId, int itemType, Vector3 pos)> OnItemSpawned { get; } = new();
        public Subject<(int itemId, int playerId, int itemType)> OnItemConsumed { get; } = new();
        public Subject<(int playerId, int emoticonId)> OnEmoticonUsed { get; } = new();
        
        public void Dispose()
        {
            IsConnected.Dispose();
            IsJoined.Dispose();
            LocalPlayerId.Dispose();
            OnPlayerUpdated.Dispose();
            OnFired.Dispose();
            OnItemSpawned.Dispose();
            OnItemConsumed.Dispose();
            OnEmoticonUsed.Dispose();
        }
    }
}