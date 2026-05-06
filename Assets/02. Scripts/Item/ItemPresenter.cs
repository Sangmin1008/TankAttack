using System;
using R3;
using TankAttack.Network.Manager;
using UnityEngine;

public class ItemPresenter : IDisposable
{
    private ItemModel _model;
    private ItemView _view;
    private readonly NetworkPresenter _netPresenter;
    private readonly NetworkModel _netModel;

    private readonly CompositeDisposable _disposables = new();

    
    public ItemPresenter(ItemModel model, ItemView view, NetworkPresenter netPresenter, NetworkModel netModel)
    {
        _model = model;
        _view = view;
        _netPresenter = netPresenter;
        _netModel = netModel;
    }

    public void Initialize()
    {
        Observable.EveryUpdate()
            .Subscribe(_ => _view.RotateItem())
            .AddTo(_disposables);

        _view.OnLocalPlayerTriggered
            .ThrottleFirst(TimeSpan.FromSeconds(1f))
            .SubscribeAwait(async (_, _) =>
            {
                Debug.Log($"[{_model.ItemId.Value}번 아이템] 획득 요청 전송 중...");
                await _netPresenter.SendItemPickupAsync(_model.ItemId.Value);
            })
            .AddTo(_disposables);
        
        _netModel.OnItemConsumed
            .Subscribe(_ => _view.PlayEffect())
            .AddTo(_disposables);
    }
    
    public void Dispose()
    {
        _disposables.Dispose();
    }
}