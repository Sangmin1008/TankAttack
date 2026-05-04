using UnityEngine;

[CreateAssetMenu(fileName = "NewTankData", menuName = "TankAttack/TankData", order = 0)]
public class TankDataSO : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float rotateSpeed = 10f;
        
    [Header("Combat")]
    public float fireForce = 1000f;
    public int maxHp = 100;
    public float respawnTime = 3f;
}