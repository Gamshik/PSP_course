using System.Collections.Generic;
using UnityEngine;

public enum EncounterType
{
    Boss,
    Puzzle_Soccer,
    Puzzle_Colors,
    Puzzle_Laser
}

public class EncounterSetup : MonoBehaviour
{
    public int Id;
    public EncounterType Type;
    public BoxCollider TriggerBounds;
    public Transform SpawnPoint;

    [Header("Boss Settings")]
    public int OverrideHp = 0; 
    public int ProjectileCount = 0;

    public float OverrideMoveSpeed = 0f;
    public float OverrideShootInterval = 0f;

    [Header("Puzzle Settings")]
    public Transform TargetZone;
    public int PoisonDamage = 1;

    [Header("Puzzle Colors Settings")]
    public List<Transform> PuzzlePoints;

    [Header("Puzzle Laser Settings")]
    public int LaserDamage = 30;
}