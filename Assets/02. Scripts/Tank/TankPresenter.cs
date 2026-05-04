using System;
using Cysharp.Threading.Tasks;
using R3;
using TankAttack.Network;
using TankAttack.Network.Manager;
using UnityEngine;
using Random = UnityEngine.Random;

public class TankPresenter : IDisposable
{
    private readonly TankModel _model;
    private readonly TankView _view;
    private readonly NetworkTransformView _ntv;
    private readonly NetworkModel _netModel;
    private readonly NetworkPresenter _netPresenter;
    
    private readonly CompositeDisposable _disposables = new();

    public TankPresenter(TankModel model, TankView view, NetworkTransformView ntv, NetworkModel netModel, NetworkPresenter netPresenter)
    {
        _model = model;
        _view = view;
        _ntv = ntv;
        _netModel = netModel;
        _netPresenter = netPresenter;
    }

    public void Initialize()
    {
        BindMovement();
        BindCombat();
        BindRespawnLogic();
    }
    private void BindMovement()
    {
        Observable.EveryUpdate()
            .Where(_ => _ntv.IsMine && !_model.IsDead.Value)
            .Subscribe(_ =>
            {
                Vector3 moveDir = _view.GetCalculatedMoveDirection();
                if (moveDir != Vector3.zero)
                {
                    _view.ApplyMovement(moveDir, _model.Data.moveSpeed, _model.Data.rotateSpeed);
                }
            }).AddTo(_disposables);
    }

    private void BindCombat()
    {
        _view.OnFireInput
            .Where(_ => _ntv.IsMine && !_model.IsDead.Value)
            .SubscribeAwait(async (_, _) =>
            {
                await _netPresenter.SendFireAsync(_ntv.PlayerId, _view.GetFirePosition(), Vector3.up * _view.GetFireRotationY());
            }).AddTo(_disposables);

        _netModel.OnFired
            .Where(packet => packet.playerId == _ntv.PlayerId)
            .Subscribe(_ => _view.FireBulletVisual(_model.Data.fireForce))
            .AddTo(_disposables);

        _view.OnHit
            .Where(_ => !_model.IsDead.Value)
            .Subscribe(damage =>
            {
                _model.CurrentHp.Value -= damage;
                if (_model.CurrentHp.Value <= 0)
                {
                    _model.IsDead.Value = true;
                }
            }).AddTo(_disposables);
    }

    private void BindRespawnLogic()
    {
        _model.IsDead
            .Where(isDead => isDead)
            .SubscribeAwait(async (_, token) => await HandleRespawnAsync(token))
            .AddTo(_disposables);
    }

    private async UniTask HandleRespawnAsync(System.Threading.CancellationToken token)
    {
        _view.SetVisible(false);

        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_model.Data.respawnTime), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        _model.CurrentHp.Value = _model.Data.maxHp;

        if (_ntv.IsMine)
        {
            _view.transform.position = new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
        }
        else
        {
            await _ntv.OnPositionReceived.FirstAsync(cancellationToken: token);
            _ntv.SnapToTarget();
        }

        _view.SetVisible(true);
        _model.IsDead.Value = false;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}