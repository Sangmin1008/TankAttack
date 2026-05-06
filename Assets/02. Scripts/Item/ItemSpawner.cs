using System;
using System.Collections.Generic;
using R3;
using TankAttack.Network.Manager;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public class ItemSpawner : IInitializable, IDisposable
{
    private readonly NetworkModel _netModel;
    private readonly IObjectResolver _resolver;
    
    private readonly Dictionary<ItemType, GameObject> _itemPrefabs = new();
    
    private readonly Dictionary<int, GameObject> _activeItems = new();
    private readonly CompositeDisposable _disposables = new();

    [Inject]
    public ItemSpawner(NetworkModel netModel, NetworkUIView view, IObjectResolver resolver)
    {
        _netModel = netModel;
        _resolver = resolver;

        _itemPrefabs[ItemType.Speed] = view.speedItemPrefab;
        _itemPrefabs[ItemType.Healing] = view.healItemPrefab;
        _itemPrefabs[ItemType.DamageBonus] = view.powerItemPrefab;
    }

    public void Initialize()
    {
        _netModel.OnItemSpawned
            .Subscribe(packet => SpawnItem(packet.itemId, (ItemType)packet.itemType, packet.pos))
            .AddTo(_disposables);

        _netModel.OnItemConsumed
            .Subscribe(packet => ConsumeItem(packet.itemId))
            .AddTo(_disposables);
    }

    private void SpawnItem(int itemId, ItemType type, Vector3 position)
    {
        if (_activeItems.ContainsKey(itemId)) return;

        if (!_itemPrefabs.TryGetValue(type, out GameObject prefabToSpawn) || prefabToSpawn == null)
        {
            Debug.LogError($"[ItemSpawner] 프리팹을 찾을 수 없습니다 타입: {type}");
            return;
        }

        var itemObj = _resolver.Instantiate(prefabToSpawn, position, Quaternion.identity);
        var itemView = itemObj.GetComponent<ItemView>();
        
        if (itemView != null)
        {
            itemView.InitSetup(itemId, type);
        }

        _activeItems[itemId] = itemObj;
    }

    private void ConsumeItem(int itemId)
    {
        if (_activeItems.TryGetValue(itemId, out var itemObj))
        {
            Object.Destroy(itemObj);
            _activeItems.Remove(itemId);
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        foreach (var item in _activeItems.Values)
        {
            if (item != null) Object.Destroy(item);
        }
        _activeItems.Clear();
    }
}