using System;
using System.Collections.Generic;
using R3;
using TankAttack.Network.Manager;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public class EmoticonManager : IInitializable, IDisposable
{
    private readonly NetworkUIView _netView;
    private readonly IObjectResolver _resolver;
    private Camera _mainCamera;

    private ObjectPool<EmoticonView> _pool;
    
    private readonly Dictionary<Transform, EmoticonView> _activeEmoticons = new();
    private readonly CompositeDisposable _disposables = new();
    
    public Sprite[] EmoticonSprites;
    
    [Inject]
    public EmoticonManager(NetworkUIView netView, IObjectResolver resolver)
    {
        _netView = netView;
        _resolver = resolver;
    }

    public void Initialize()
    {
        _mainCamera = Camera.main;
        
        EmoticonSprites = _netView.emoticonSprites; 

        _pool = new ObjectPool<EmoticonView>(
            createFunc: CreateView,
            actionOnGet: v => { if (v != null) v.gameObject.SetActive(true); },
            actionOnRelease: v => { if (v != null) v.gameObject.SetActive(false); },
            actionOnDestroy: v => { if (v != null && v.gameObject != null) Object.Destroy(v.gameObject); }
        );

        Observable.EveryUpdate(UnityFrameProvider.PostLateUpdate)
            .Subscribe(_ => UpdatePositions())
            .AddTo(_disposables);
    }

    private EmoticonView CreateView()
    {
        var obj = _resolver.Instantiate(_netView.emoticonPrefab, _netView.globalCanvasRect);
        obj.transform.localScale = Vector3.one;
        var view = obj.GetComponent<EmoticonView>();

        view.OnFinished
            .Subscribe(v => 
            {
                _pool.Release(v);
                Transform targetToRemove = null;
                foreach(var kvp in _activeEmoticons) { if (kvp.Value == v) targetToRemove = kvp.Key; }
                if (targetToRemove != null) _activeEmoticons.Remove(targetToRemove);
            })
            .AddTo(view.gameObject);

        return view;
    }

    public void ShowEmoticon(Transform target, int emoticonId)
    {
        if (emoticonId < 1 || emoticonId > EmoticonSprites.Length) return;

        Sprite selectedSprite = EmoticonSprites[emoticonId - 1];

        if (_activeEmoticons.TryGetValue(target, out var existingView))
        {
            existingView.PlayAnimation(selectedSprite).Forget();
        }
        else
        {
            var newView = _pool.Get();
            _activeEmoticons[target] = newView;
            newView.PlayAnimation(selectedSprite).Forget();
        }
    }

    private void UpdatePositions()
    {
        if (_mainCamera == null) return;

        foreach (var kvp in _activeEmoticons)
        {
            Transform target = kvp.Key;
            EmoticonView view = kvp.Value;

            if (target == null) continue;

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(target.position + Vector3.up * 4.0f);
            
            if (screenPos.z > 0)
            {
                if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);
                view.RectTransform.position = screenPos;
            }
            else
            {
                if (view.gameObject.activeSelf) view.gameObject.SetActive(false);
            }
        }
    }

    public void ClearAll()
    {
        _activeEmoticons.Clear();
        _pool?.Clear();
    }

    public void Dispose()
    {
        ClearAll();
        _disposables.Dispose();
    }
}