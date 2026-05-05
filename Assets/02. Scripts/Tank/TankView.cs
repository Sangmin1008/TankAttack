using System.Collections.Generic;
using R3;
using TankAttack.Network;
using TankAttack.Network.Manager;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

public class TankView : MonoBehaviour
{
    [SerializeField] private TankDataSO tankData;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePos;
    
    private Camera _mainCamera;
    private InputSystem_Actions _inputActions;
    private List<MeshRenderer> _meshRenderers = new();
    private Collider _collider;
    private Rigidbody _rigidbody;
    private NetworkTransformView _ntv;
    
    public Subject<Unit> OnFireInput { get; } = new();
    public Subject<int> OnHit { get; } = new();
    
    private TankModel _model;
    private TankPresenter _presenter;
    private NetworkModel _netModel;
    
    [Inject]
    public void Construct(NetworkModel netModel, NetworkPresenter netPresenter)
    {
        _ntv = GetComponent<NetworkTransformView>();
        
        _netModel = netModel;
        
        _model = new TankModel(tankData);
        _presenter = new TankPresenter(_model, this, _ntv, netModel, netPresenter);
        
        _presenter.Initialize();
    }

    #region 유니티 생명주기 및 세팅
    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
        _inputActions.Player.Attack.started += _ => OnFireInput.OnNext(Unit.Default);
        
        GetComponentsInChildren<MeshRenderer>(_meshRenderers);
        _collider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable() => _inputActions.Enable();
    private void OnDisable() => _inputActions.Disable();

    private void Start()
    {
        _mainCamera = Camera.main;

        if (_ntv.IsMine)
        {
            FindFirstObjectByType<CinemachineCamera>().Follow = transform;
        }
        else
        {
            _rigidbody.isKinematic = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BULLET"))
        {
            var bullet = other.GetComponent<Bullet>();
            if (bullet.OwnerId == _netModel.LocalPlayerId.Value)
            {
                OnHit.OnNext(25);
            }
        }
    }

    private void OnDestroy()
    {
        _presenter?.Dispose();
        _model?.Dispose();
        OnFireInput.Dispose();
        OnHit.Dispose();
    }
    #endregion

    #region Presenter가 호출할 시각적/물리적 명령들
    
    // 현재 키보드 입력값을 카메라 방향에 맞게 변환하여 반환
    public Vector3 GetCalculatedMoveDirection()
    {
        Vector2 input = _inputActions.Player.Move.ReadValue<Vector2>();
        if (input.magnitude < 0.1f) return Vector3.zero;

        Vector3 camForward = _mainCamera.transform.forward;
        Vector3 camRight = _mainCamera.transform.right;
        camForward.y = 0f; camRight.y = 0f;

        return (camForward * input.y) + (camRight * input.x);
    }

    public void ApplyMovement(Vector3 moveDir, float moveSpeed, float rotateSpeed)
    {
        transform.Translate(moveDir.normalized * moveSpeed * Time.deltaTime, Space.World);
        Quaternion targetRot = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    public void FireBulletVisual(float fireForce, int ownerId)
    {
        // TODO 오브젝트 풀링 + Bullet 구조체를 매개변수로 넘기기
        var bullet = Instantiate(bulletPrefab, firePos.position, firePos.rotation);
        var bulletScript = bullet.AddComponent<Bullet>();
        bulletScript.OwnerId = ownerId;
        bullet.GetComponent<Rigidbody>().AddRelativeForce(Vector3.forward * fireForce);
    }

    public void SetVisible(bool isVisible)
    {
        foreach (var mr in _meshRenderers) mr.enabled = isVisible;
        _collider.enabled = isVisible;
        _rigidbody.useGravity = isVisible;
    }

    public Vector3 GetFirePosition() => firePos.position;
    public float GetFireRotationY() => firePos.rotation.eulerAngles.y;
    #endregion
}