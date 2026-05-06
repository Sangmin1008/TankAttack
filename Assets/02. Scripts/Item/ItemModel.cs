using UnityEngine;
using R3;

public class ItemModel
{
    public ReactiveProperty<int> ItemId { get; } = new(-1);
    public ReactiveProperty<ItemType> Type { get; } = new(ItemType.Speed);
}