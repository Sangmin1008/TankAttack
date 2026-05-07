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
    private readonly HpBarManager _hpBarManager;
    private readonly DamageTextManager _damageTextManager;
    
    private readonly CompositeDisposable _disposables = new();

    private HpBarView _hpBarView;
    
    public TankPresenter(TankModel model, TankView view, NetworkTransformView ntv, NetworkModel netModel, NetworkPresenter netPresenter, HpBarManager hpBarManager, DamageTextManager damageTextManager)
    {
        _model = model;
        _view = view;
        _ntv = ntv;
        _netModel = netModel;
        _netPresenter = netPresenter;
        _hpBarManager = hpBarManager;
        _damageTextManager = damageTextManager;
    }

    public void Initialize()
    {
        BindHpBar();
        BindMovement();
        BindCombat();
        BindRespawnLogic();
        BindItems();
    }

    private void BindHpBar()
    {
        _hpBarView = _hpBarManager.RegisterHpBar(_view.transform);
        
        _model.CurrentHp
            .Subscribe(hp =>
            {
                if (_hpBarView == null) return;
                _hpBarView.UpdateValue(hp, _model.Data.maxHp);
            })
            .AddTo(_disposables);
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
                    // _view.ApplyMovement(moveDir, _model.Data.moveSpeed, _model.Data.rotateSpeed);
                    float currentSpeed = _model.HasSpeedBuff.Value ? _model.Data.moveSpeed * 2f : _model.Data.moveSpeed;
                    _view.ApplyMovement(moveDir, currentSpeed, _model.Data.rotateSpeed);
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
            .Subscribe(packet => 
            {
                int damage = _model.HasPowerBuff.Value ? 50 : 25;
                _view.FireBulletVisual(_model.Data.fireForce, packet.playerId, damage);
            }).AddTo(_disposables);
        
        _view.OnHit
            .SubscribeAwait(async (damage, _) =>
            {
                await _netPresenter.SendPlayerHitAsync(_ntv.PlayerId, damage);
            }).AddTo(_disposables);
        
        _netModel.OnPlayerHit
            .Where(packet => packet.targetId == _ntv.PlayerId && !_model.IsDead.Value)
            .Subscribe(packet =>
            {
                _damageTextManager.SpawnText(packet.damage, _view.transform.position);
                _model.CurrentHp.Value -= packet.damage;
                if (_model.CurrentHp.Value <= 0)
                {
                    _model.IsDead.Value = true;
                }
            })
            .AddTo(_disposables);
    }

    private void BindRespawnLogic()
    {
        _model.IsDead
            .Where(isDead => isDead)
            .SubscribeAwait(async (_, token) => 
            {
                _hpBarManager.UnregisterHpBar(_view.transform);
                _hpBarView = null;
                await HandleRespawnAsync(token);

                if (!_model.IsDead.Value)
                {
                    _hpBarView = _hpBarManager.RegisterHpBar(_view.transform);
                    _hpBarView.UpdateValue(_model.CurrentHp.Value, _model.Data.maxHp);
                }
            })
            .AddTo(_disposables);
    }

    private void BindItems()
    {
        _netModel.OnItemConsumed
            .Where(packet => packet.playerId == _ntv.PlayerId && !_model.IsDead.Value)
            .Subscribe(packet => ApplyItemEffect((ItemType)packet.itemType))
            .AddTo(_disposables);
    }

    private void ApplyItemEffect(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Healing:
                _model.CurrentHp.Value = Mathf.Min(_model.CurrentHp.Value + 50, _model.Data.maxHp);
                _damageTextManager.SpawnText(50, _view.transform.position, true);
                Debug.Log($"[{_ntv.PlayerId}] 체력 회복! 현재 체력: {_model.CurrentHp.Value}");
                break;
                    
            case ItemType.Speed:
                _model.HasSpeedBuff.Value = true;
                Debug.Log($"[{_ntv.PlayerId}] 7초간 이동 속도 2배!");
                    
                Observable.Timer(TimeSpan.FromSeconds(7f))
                    .Subscribe(_ => _model.HasSpeedBuff.Value = false)
                    .AddTo(_disposables);
                break;
                    
            case ItemType.DamageBonus:
                _model.HasPowerBuff.Value = true;
                Debug.Log($"[{_ntv.PlayerId}] 7초간 공격력 2배!");
                    
                Observable.Timer(TimeSpan.FromSeconds(7f))
                    .Subscribe(_ => _model.HasPowerBuff.Value = false)
                    .AddTo(_disposables);
                break;
        }
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
            await _ntv.OnPositionReceived.FirstOrDefaultAsync(cancellationToken: token);
            _ntv.SnapToTarget();
        }

        _view.SetVisible(true);
        _model.IsDead.Value = false;
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _model.Dispose();
    }
}