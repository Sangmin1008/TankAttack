using System;
using System.Collections.Generic;
using R3;
using TankAttack.Network.Manager;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public class HpBarManager : IInitializable, IDisposable
{
    private readonly NetworkUIView _netView;
    private readonly IObjectResolver _resolver;
    private Camera _mainCamera;

    private readonly Dictionary<Transform, HpBarView> _hpBars = new();
    private readonly CompositeDisposable _disposables = new();

    [Inject]
    public HpBarManager(NetworkUIView netView, IObjectResolver resolver)
    {
        _netView = netView;
        _resolver = resolver;
    }
    
    public void Initialize()
    {
        _mainCamera = Camera.main;
        
        Observable.EveryUpdate(UnityFrameProvider.PostLateUpdate)
            .Subscribe(_ => UpdateHpBarPositions())
            .AddTo(_disposables);
    }

    public HpBarView RegisterHpBar(Transform target)
    {
        var obj = _resolver.Instantiate(_netView.hpBarPrefab, _netView.globalCanvasRect);
        var hpBarView = obj.GetComponent<HpBarView>();
        
        _hpBars[target] = hpBarView;
        return hpBarView;
    }

    public void UnregisterHpBar(Transform target)
    {
        if (_hpBars.TryGetValue(target, out var hpBarView))
        {
            if (hpBarView != null) Object.Destroy(hpBarView.gameObject);
            _hpBars.Remove(target);
        }
    }

    private void UpdateHpBarPositions()
    {
        if (_mainCamera == null) return;

        foreach (var kvp in _hpBars)
        {
            Transform target = kvp.Key;
            HpBarView hpBarView = kvp.Value;

            if (target == null || hpBarView == null) continue;

            Vector3 worldPos = target.position + (Vector3.up * 1.5f) + (Vector3.right * 1.5f);
            
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z > 0)
            {
                if (!hpBarView.gameObject.activeSelf) hpBarView.gameObject.SetActive(true);
                hpBarView.RectTransform.position = screenPos;
            }
            else
            {
                if (hpBarView.gameObject.activeSelf) hpBarView.gameObject.SetActive(false);
            }
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        foreach (var hpBar in _hpBars.Values)
        {
            if (hpBar != null) Object.Destroy(hpBar.gameObject);
        }
        _hpBars.Clear();
    }
}