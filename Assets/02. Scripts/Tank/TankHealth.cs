using System.Collections;
using System.Collections.Generic;
using TankAttack.Network;
using UnityEngine;

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
                StartCoroutine(Respawn());
            }
        }
    }

    private IEnumerator Respawn()
    {
        // 탱크 비활성화
        SetVisible(false);
        yield return new WaitForSeconds(respawnTime);
        
        // 체력 회복
        currentHp = maxHp;
        
        if (_ntv.IsMine)
        {
            // 랜덤 위치로 변경
            Vector3 respawnPos = new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
            transform.position = respawnPos;
        }

        yield return null;
        SetVisible(true);
    }
}
