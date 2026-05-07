using System;
using TankAttack.Network.Manager;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public class DamageTextManager : IInitializable, IDisposable
{
    private ObjectPool<DamageTextView> _pool;
    private readonly NetworkUIView _view;
    private readonly IObjectResolver _resolver;
    private Camera _mainCamera;
    
    [Inject]
    public DamageTextManager(NetworkUIView view, IObjectResolver resolver)
    {
        _view = view;
        _resolver = resolver;
    }
    
    public void Initialize()
    {
        _mainCamera = Camera.main;

        _pool = new ObjectPool<DamageTextView>(
            createFunc: CreateNewText,
            actionOnGet: text => 
            { 
                if (text != null) text.gameObject.SetActive(true); 
            },
            actionOnRelease: text => 
            { 
                if (text != null) text.gameObject.SetActive(false); 
            },
            actionOnDestroy: text => 
            { 
                if (text != null && text.gameObject != null) 
                {
                    Object.Destroy(text.gameObject);
                }
            },
            defaultCapacity: 20,
            maxSize: 50
        );
    }
    
    private DamageTextView CreateNewText()
    {
        var obj = _resolver.Instantiate(_view.damageTextPrefab, _view.globalCanvasRect);
        obj.transform.localScale = Vector3.one;
        return obj.GetComponent<DamageTextView>();
    }

    public void SpawnText(int amount, Vector3 worldPosition, bool isHeal = false)
    {
        if (_mainCamera == null) return;

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPos.z <= 0) return;

        var view = _pool.Get();

        float randomOffsetX = UnityEngine.Random.Range(-30f, 30f);
        screenPos.x += randomOffsetX;
        view.transform.position = screenPos;

        view.Init(amount, isHeal, textToReturn => _pool.Release(textToReturn));
        view.PlayAnimation().Forget();
    }
    
    

    public void Dispose()
    {
        _pool?.Dispose();
    }
}