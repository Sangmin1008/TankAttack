using System;
using TankAttack.Network;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class TankController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private float fireForce = 1000f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePos;
    
    private InputSystem_Actions _inputActions;
    private InputAction _moveAction;
    private InputAction _fireAction;
    
    private Vector2 _moveInput;
    private Vector3 _moveDir;

    private NetworkTransformView _ntv;
    private CinemachineCamera _cmCamera;


    #region 유니티 생명 주기

    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
        _moveAction = _inputActions.Player.Move;
        _fireAction = _inputActions.Player.Attack;
    }

    private void OnEnable()
    {
        _moveAction.Enable();
        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;
        
        _fireAction.Enable();
        _fireAction.started += OnFire;

        NetworkManager.Instance.OnFired += OnFired;
    }


    private void OnDisable()
    {
        _moveAction.performed -= OnMove;
        _moveAction.canceled -= OnMove;
        _moveAction.Disable();
        
        _fireAction.started -= OnFire;
        _fireAction.Disable();
        
        NetworkManager.Instance.OnFired -= OnFired;
    }

    private void OnFired(int firePlayerId, Vector3 pos, Vector3 rot)
    {
        if (_ntv.PlauerId != firePlayerId) return;
        FireBullet();
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        _ntv = GetComponent<NetworkTransformView>();
        _cmCamera = FindFirstObjectByType<CinemachineCamera>();

        if (_ntv.IsMine)
        {
            _cmCamera.Follow = transform;
        }
        else
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }
        
        firePos = transform.Find("FirePos");
    }

    private void Update()
    {
        if (!_ntv.IsMine) return;
        Movement();
    }
    
    #endregion

    #region 이동 처리 및 발사 로직

    private void Movement()
    {
        _moveDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
        if (_moveDir.magnitude > 0.1f)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            
            
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            
            
            _moveDir = (cameraForward * _moveInput.y) + (cameraRight * _moveInput.x);
            transform.Translate(_moveDir.normalized * moveSpeed * Time.deltaTime, Space.World);
            
            Quaternion targetRot = Quaternion.LookRotation(_moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region 발사 로직

    private void FireBullet()
    {
        var bullet = Instantiate(bulletPrefab, firePos.position, firePos.rotation);
        bullet.GetComponent<Rigidbody>().AddRelativeForce(Vector3.forward * fireForce);
    }

    #endregion

    #region 이벤트 핸들러

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private async void OnFire(InputAction.CallbackContext ctx)
    {
        if (!_ntv.IsMine) return;
        await NetworkManager.Instance.SendFireAsync(_ntv.PlauerId, firePos.position, Vector3.up * firePos.rotation.eulerAngles.y);
        // FireBullet();
    }
    
    #endregion
}
