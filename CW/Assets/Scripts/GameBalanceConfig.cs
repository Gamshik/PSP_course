using UnityEngine;

[CreateAssetMenu(fileName = "NewGameBalance", menuName = "Game/Balance Config")]
public class GameBalanceConfig : ScriptableObject
{
    [Header("General")]
    public float MoveSpeed = 6f;

    [Header("Melee (Sword)")]
    public int MeleeBaseDamage = 20;
    public int MeleeDamagePerLevel = 10;
    public float MeleeCooldown = 0.8f;
    public float MeleeRadius = 2.5f;
    public float MeleeAngle = 90f;
    public float MeleeAnimDuration = 0.2f;

    [Header("Range (Bow)")]
    public int RangeDamage = 15;
    public float RangeBaseFireRate = 1.0f;
    public float RangeFireRateReductionPerLevel = 0.2f;
    public float ProjectileSpeed = 12f;
    public float ProjectileLifeTime = 3.0f; 

    [Header("Defense (Armor)")]
    public int BaseHp = 100;
    public int HpPerLevel = 25;

    [Header("Support (Medkit)")]
    public int HealBaseAmount = 30;
    public int HealPerLevel = 15;
    public float HealCooldown = 10.0f;
    public float HealDistance = 4.0f;

    [Header("Boss Settings")]
    public int BossHp = 500;
    public float BossMoveSpeed = 3f;
    public int BossContactDamage = 30;
    public float BossKnockbackForce = 5f; 

    [Header("Boss Attack")]
    public int BossProjectileDamage = 15;
    public float BossProjectileSpeed = 8f;
    public float BossShootInterval = 2.0f; 
    public int BossProjectilesCount = 12; 

    [Header("Puzzle Settings")]
    public float PuzzleCubePushForce = 2.0f;
    public float PuzzleCubeRadius = 1.0f;   

    [Header("Puzzle Colors Settings")]
    public float ColorShowInterval = 1.0f; 
    public int ColorBaseSequenceLength = 3;
    public int ColorDamageOnFail = 20; 
    public float ColorRoundDelay = 2.0f; 

    [Header("Puzzle Laser Settings")]
    public float LaserTrackingSpeed = 15f;
    public float LaserChargeSpeed = 15f;  
    public float LaserLength = 20f;       
    public float LaserWidthAngle = 10f;
    public int LaserDamage = 1;
}