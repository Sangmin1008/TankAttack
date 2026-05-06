using System;
using R3;
using TankAttack.Network;
using TankAttack.Network.Manager;
using UnityEngine;
using VContainer;

public class ItemView : MonoBehaviour
{
    [SerializeField] private ParticleSystem collectFx;
    
    private ItemModel _model;
    private ItemPresenter _presenter;
    
    public Subject<Unit> OnLocalPlayerTriggered { get; } = new();
    
    [Inject]
    public void Construct(NetworkPresenter netPresenter, NetworkModel netModel)
    {
        _model = new ItemModel();
        _presenter = new ItemPresenter(_model, this, netPresenter, netModel);
        
        _presenter.Initialize();
    }

    public void InitSetup(int itemId, ItemType type)
    {
        _model.ItemId.Value = itemId;
        _model.Type.Value = type;
    }

    private void OnDestroy()
    {
        _presenter?.Dispose();
        OnLocalPlayerTriggered.Dispose();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ground"))
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // rb.linearVelocity = Vector3.zero;
                // rb.angularVelocity = Vector3.zero;
                
                rb.useGravity = false;
                rb.isKinematic = true;
            }
        }
        if (other.CompareTag("Player"))
        {
            var ntv = other.GetComponent<NetworkTransformView>();
            if (ntv != null && ntv.IsMine)
            {
                OnLocalPlayerTriggered.OnNext(Unit.Default);
                
            }
        }
    }

    public void RotateItem()
    {
        transform.rotation = Quaternion.Euler(0, 50f * Time.time, 0);
    }

    public void PlayEffect()
    {
        if (collectFx != null)
            Instantiate(collectFx, transform.position, Quaternion.identity);
    }
}