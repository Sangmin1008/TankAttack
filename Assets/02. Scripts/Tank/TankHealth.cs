using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TankAttack.Network;
using UnityEngine;
using Random = UnityEngine.Random;

public class TankHealth : MonoBehaviour
{
    [SerializeField] private int currentHp = 100;
    [SerializeField] private int maxHp = 100;
    [SerializeField] private float respawnTime = 3f;
    
    private const string BULLET_TAG = "BULLET";

    private List<MeshRenderer> _meshRenderers = new();
    private Collider _collider;
    private Rigidbody _rigidbody;
    
    private NetworkTransformView _ntv;
    
    void Start()
    {
        GetComponentsInChildren<MeshRenderer>(_meshRenderers);
        _collider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
        
        _ntv = GetComponent<NetworkTransformView>();
    }

    private void SetVisible(bool visible)
    {
        foreach (var meshRenderer in _meshRenderers)
        {
            meshRenderer.enabled = visible;
        }
        _collider.enabled = visible;
        _rigidbody.useGravity = visible;
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag(BULLET_TAG))
        {
            currentHp -= 25;
            if (currentHp <= 0)
            {
                // 리프손 로직
                RespawnAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
        }
    }

    private async UniTask RespawnAsync(CancellationToken token)
    {
        // 탱크 비활성화
        SetVisible(false);
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(respawnTime), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;
        
        // 체력 회복
        currentHp = maxHp;
        
        if (_ntv.IsMine)
        {
            // 랜덤 위치로 변경
            Vector3 respawnPos = new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
            transform.position = respawnPos;
            SetVisible(true);
        }
        else
        {
            // 💡 R3의 FirstAsync()를 사용한 극강의 가독성!
            // FirstAsync는 첫 번째 이벤트(패킷)가 들어올 때까지만 기다리고 자동으로 해제됩니다.
            await _ntv.OnPositionReceived.FirstAsync(cancellationToken: token);
            
            _ntv.SnapToTarget();
            SetVisible(true);
        }

        
    }
}
