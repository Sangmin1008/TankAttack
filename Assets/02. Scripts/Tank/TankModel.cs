using System;
using R3;
using UnityEngine;

public class TankModel : IDisposable
{
    public TankDataSO Data { get; }
    
    public ReactiveProperty<int> CurrentHp { get; }
    public ReactiveProperty<bool> IsDead { get; } = new(false);
    public ReactiveProperty<bool> HasSpeedBuff { get; } = new(false);
    public ReactiveProperty<bool> HasPowerBuff { get; } = new(false);

    public TankModel(TankDataSO data)
    {
        Data = data;
        CurrentHp = new ReactiveProperty<int>(data.maxHp);
    }
    
    public void Dispose()
    {
        CurrentHp.Dispose();
        IsDead.Dispose();
        HasSpeedBuff.Dispose();
        HasPowerBuff.Dispose();
    }
}