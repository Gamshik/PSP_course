using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Configs")]
    public GameBalanceConfig Balance;

    [Header("Prefabs")]
    public PlayerVisualController PlayerPrefab;
    public GameObject BulletPrefab;

    [Header("Boss Prefabs")]
    public BossVisualController BossPrefab;
    public GameObject BossBulletPrefab;

    [Header("Puzzle Prefabs")]
    public PuzzleCubeVisual PuzzleBallPrefab; 

    public GameUIController UI;

    [Header("Level Setup")]
    public List<EncounterSetup> Encounters;

    private class ServerPlayer
    {
        public PlayerInfo Info;
        public Vector3 Position;
        public Quaternion Rotation;
        public int CurrentHp, MaxHp;
        public bool IsDead;
        public float MeleeTimer, RangeTimer, HealTimer;
        public int MeleeDmg, RangeDmg, HealAmount;
        public float RangeFireRate;
        public Vector3 InputMove, InputLook;
        public System.DateTime LastPacketTime;
        public float InvulnerabilityTimer;
    }

    private class ServerProjectile { public Vector3 Position, Direction; public int Damage, OwnerSlot; }

    private class ServerEncounter
    {
        public int Id;
        public bool IsActive;
        public bool IsCompleted;
        public bool IsDead;

        // Общие данные
        public Vector3 Position;
        public float RotationY;

        // Босс
        public int CurrentHp, MaxHp;
        public float ShootTimer;

        public float MoveSpeed;      
        public float ShootInterval;  

        // Пазл
        public float PoisonTimer;

        public EncounterSetup Setup;

        public int PuzzlePhase; // 0=Show, 1=Input, 2=Fail
        public List<int> ColorSequence = new List<int>(); 
        public int CurrentStep; 
        public int CurrentStreak; 
        public float PuzzleTimer; 

        public List<float> LaserAngles = new List<float>();
        public float[] PlatesProgress;
        public bool[] PlatesActiveState;
    }

    private class ServerBossProjectile { public Vector3 Position, Direction; }

    private List<ServerPlayer> _serverPlayers = new List<ServerPlayer>();
    private List<ServerProjectile> _playerProjectiles = new List<ServerProjectile>();

    private List<ServerEncounter> _allEncounters = new List<ServerEncounter>();
    private int _activeEncounterIndex = -1;

    private List<ServerBossProjectile> _bossProjectiles = new List<ServerBossProjectile>();

    private Dictionary<int, PlayerVisualController> _visualPlayers = new Dictionary<int, PlayerVisualController>();
    private List<GameObject> _visualProjectiles = new List<GameObject>();
    private ColorPuzzleVisuals _visualColorPuzzle; 
    public ColorPuzzleVisuals ColorPuzzlePrefab;   

    private BossVisualController _visualBoss;
    private PuzzleCubeVisual _visualPuzzleBall; 
    public LaserPuzzleVisuals PuzzleLaserPrefab;
    private LaserPuzzleVisuals _visualLaserPuzzle;

    private List<GameObject> _visualBossBullets = new List<GameObject>();

    private bool _isGameRunning = false;
    private Color[] _colors = { Color.red, Color.blue, Color.green, Color.yellow };
    private float _handshakeTimer = 0f;
    private System.DateTime _lastServerHeartbeat;

    private void Start()
    {
        var players = NetworkManager.Instance.FinalPlayerList;
        if (players == null || players.Count == 0) return;

        NetworkManager.Instance.OnDataReceived += HandleData;
        _lastServerHeartbeat = System.DateTime.Now;

        for (int i = 0; i < players.Count; i++)
        {
            int slot = players[i].SlotIndex;
            SpawnVisualPlayer(slot, players[i]);

            if (NetworkManager.Instance.IsServer)
            {
                ServerPlayer sp = new ServerPlayer();
                sp.Info = players[i];
                sp.Position = new Vector3((slot - 1.5f) * 3, 0, 0);
                sp.Rotation = Quaternion.identity;

                int[] l = players[i].GearLevels;
                sp.MaxHp = Balance.BaseHp + (l[2] * Balance.HpPerLevel); sp.CurrentHp = sp.MaxHp;
                sp.MeleeDmg = Balance.MeleeBaseDamage + (l[0] * Balance.MeleeDamagePerLevel);
                sp.RangeFireRate = Balance.RangeBaseFireRate - (l[1] * Balance.RangeFireRateReductionPerLevel);
                sp.RangeDmg = Balance.RangeDamage;
                sp.HealAmount = Balance.HealBaseAmount + (l[3] * Balance.HealPerLevel);
                sp.LastPacketTime = System.DateTime.Now;
                _serverPlayers.Add(sp);
            }
        }

        var bObj = Instantiate(BossPrefab);
        _visualBoss = bObj.GetComponent<BossVisualController>();
        _visualBoss.gameObject.SetActive(false);

        if (PuzzleBallPrefab != null)
        {
            var pObj = Instantiate(PuzzleBallPrefab);
            _visualPuzzleBall = pObj.GetComponent<PuzzleCubeVisual>();
            _visualPuzzleBall.gameObject.SetActive(false);
        }
        if (ColorPuzzlePrefab != null)
        {
            var cObj = Instantiate(ColorPuzzlePrefab);
            _visualColorPuzzle = cObj.GetComponent<ColorPuzzleVisuals>();
            _visualColorPuzzle.gameObject.SetActive(false);
        }
        if (PuzzleLaserPrefab != null)
        {
            var lObj = Instantiate(PuzzleLaserPrefab);
            _visualLaserPuzzle = lObj.GetComponent<LaserPuzzleVisuals>();
            _visualLaserPuzzle.gameObject.SetActive(false);
        }

        if (NetworkManager.Instance.IsServer)
        {
            // инициализируем энкаунтеры
            for (int i = 0; i < Encounters.Count; i++)
            {
                var setup = Encounters[i];
                ServerEncounter se = new ServerEncounter();
                se.Id = i;
                se.Setup = setup;
                se.IsActive = false;
                se.IsCompleted = false;
                se.IsDead = false;
                se.Position = setup.SpawnPoint.position;

                if (setup.Type == EncounterType.Boss)
                {
                    se.MaxHp = setup.OverrideHp > 0 ? setup.OverrideHp : Balance.BossHp;
                    se.CurrentHp = se.MaxHp;

                    se.MoveSpeed = setup.OverrideMoveSpeed > 0 ? setup.OverrideMoveSpeed : Balance.BossMoveSpeed;

                    se.ShootInterval = setup.OverrideShootInterval > 0 ? setup.OverrideShootInterval : Balance.BossShootInterval;
                }
                if (setup.Type == EncounterType.Puzzle_Laser)
                {
                    int platesCount = setup.PuzzlePoints != null ? setup.PuzzlePoints.Count : 0;
                    se.PlatesProgress = new float[platesCount];
                    se.PlatesActiveState = new bool[platesCount];

                    for (int p = 0; p < players.Count; p++)
                    {
                        se.LaserAngles.Add(p * (360f / players.Count));
                    }
                }

                _allEncounters.Add(se);
            }

            UI.HideWaitingPanel();
            _isGameRunning = true;
            SendMatchStartPacket();
        }
    }

    private void OnDestroy() { if (NetworkManager.Instance) NetworkManager.Instance.OnDataReceived -= HandleData; }

    private void Update()
    {
        if (!NetworkManager.Instance.IsServer && !_isGameRunning)
        {
            _handshakeTimer -= Time.deltaTime;
            if (_handshakeTimer <= 0) { _handshakeTimer = 0.5f; NetworkManager.Instance.SendPacket(new byte[] { (byte)PacketType.ClientSceneLoaded }); }
            return;
        }
        if (!_isGameRunning) return;

        if (!NetworkManager.Instance.IsServer)
        {
            if ((System.DateTime.Now - _lastServerHeartbeat).TotalSeconds > 5.0) { ForceDisconnect("Timeout"); return; }
        }

        HandleLocalInput();

        if (NetworkManager.Instance.IsServer)
        {
            ServerUpdate();
            SyncVisualsHost();
        }
    }

    /// <summary>
    /// Выполняет серверную лоигку на хосте
    /// </summary>
    private void ServerUpdate()
    {
        if (Time.frameCount % 60 == 0)
        {
            for (int i = _serverPlayers.Count - 1; i >= 0; i--)
            {
                var p = _serverPlayers[i];
            
                if (p.Info.IpEndpointStr == "Host") continue;

                if ((System.DateTime.Now - p.LastPacketTime).TotalSeconds > 5.0)
                {
                    _serverPlayers.RemoveAt(i);
                }
            }
        }

        if (_activeEncounterIndex == -1)
        {
            foreach (var enc in _allEncounters)
            {
                if (enc.IsCompleted) continue;
                foreach (var p in _serverPlayers)
                {
                    if (enc.Setup.TriggerBounds.bounds.Contains(p.Position))
                    {
                        ActivateEncounter(enc, p.Position);
                        break;
                    }
                }
                if (_activeEncounterIndex != -1) break;
            }
        }
        else
        {
            ServerEncounter active = _allEncounters.Find(e => e.Id == _activeEncounterIndex);
            if (active != null)
            {
                if (active.Setup.Type == EncounterType.Boss)
                {
                    UpdateBossBehavior(active);
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Soccer)
                {
                    UpdatePuzzleBehavior(active);
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Colors)
                {
                    UpdateColorPuzzleBehavior(active);
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Laser)
                {
                    UpdateLaserPuzzleBehavior(active);
                }

                // ограничение арена, зажимаем игроков, только если энкаунтер не завершён
                if (!active.IsCompleted)
                {
                    Bounds b = active.Setup.TriggerBounds.bounds;
                    foreach (var p in _serverPlayers)
                    {
                        p.Position.x = Mathf.Clamp(p.Position.x, b.min.x + 1, b.max.x - 1);
                        p.Position.z = Mathf.Clamp(p.Position.z, b.min.z + 1, b.max.z - 1);
                    }
                }
            }
        }

        // движение игроков
       foreach (var p in _serverPlayers)
        {
            if (p.IsDead) continue;
            
            if (p.MeleeTimer > 0) p.MeleeTimer -= Time.deltaTime;
            if (p.RangeTimer > 0) p.RangeTimer -= Time.deltaTime;
            if (p.HealTimer > 0) p.HealTimer -= Time.deltaTime;
            if (p.InvulnerabilityTimer > 0) p.InvulnerabilityTimer -= Time.deltaTime;

            // Сброс высоты
            p.Position.y = 0;

            Vector3 movement = p.InputMove * Balance.MoveSpeed * Time.deltaTime;

            // Даже если игрок не жмет кнопки, мы проверяем, не застрял ли он в стене
            // (если его туда втолкнули)
            bool isTryingToMove = movement.sqrMagnitude > 0.0001f;
            
            // Точка проверки
            Vector3 origin = p.Position + Vector3.up * 1.0f;
            float checkRadius = 0.4f; 
            
            // Если стоим на месте, проверяем минимальную дистанцию, чтобы вытолкнуть из стены
            float checkDist = isTryingToMove ? movement.magnitude : 0.1f; 
            Vector3 checkDir = isTryingToMove ? movement.normalized : p.Rotation * Vector3.forward;

            if (Physics.SphereCast(
                origin,
                checkRadius,
                checkDir,
                out RaycastHit hit,
                checkDist + 0.1f,
                LayerMask.GetMask("Obstacle"),
                QueryTriggerInteraction.Ignore))
            {
                // Если мы слишком близко к стене (или внутри неё)
                if (hit.distance < 0.05f)
                {
                    // Толкаем игрока ОТ стены по нормали
                    Vector3 pushOut = hit.normal * (0.05f - hit.distance);
                    pushOut.y = 0;
                    p.Position += pushOut;
                }

                if (isTryingToMove)
                {
                    // Подходим к стене
                    float distToWall = Mathf.Max(0, hit.distance - 0.01f);
                    p.Position += movement.normalized * distToWall;

                    // Скользим
                    Vector3 slideMovement = Vector3.ProjectOnPlane(movement, hit.normal);
                    slideMovement.y = 0;

                    // Проверка второй стены (Угол)
                    Vector3 slideOrigin = p.Position + Vector3.up * 1.0f;
                    float slideDist = slideMovement.magnitude;

                    if (slideDist > 0.0001f)
                    {
                        if (!Physics.SphereCast(slideOrigin, checkRadius, slideMovement.normalized, out RaycastHit slideHit, slideDist + 0.1f, LayerMask.GetMask("Obstacle"), QueryTriggerInteraction.Ignore))
                        {
                            p.Position += slideMovement;
                        }
                    }
                }
            }
            else if (isTryingToMove)
            {
                // Путь свободен
                p.Position += movement;
            }

            // Финальная страховка Y
            p.Position.y = 0;

            // Поворот
            Vector3 dir = p.InputLook - p.Position;
            if (dir != Vector3.zero) p.Rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
        }

        // коллизии между игроками
        float playerRadius = 0.8f;
        for (int i = 0; i < _serverPlayers.Count; i++)
        {
            for (int j = i + 1; j < _serverPlayers.Count; j++)
            {
                var p1 = _serverPlayers[i];
                var p2 = _serverPlayers[j];
                if (p1.IsDead || p2.IsDead) continue;

                float dist = Vector3.Distance(p1.Position, p2.Position);
                if (dist < playerRadius * 2)
                {
                    Vector3 dir = (p1.Position - p2.Position).normalized;
                    if (dir == Vector3.zero) dir = Vector3.right;

                    float pushDist = (playerRadius * 2 - dist) * 0.5f;
                    Vector3 pushVector = dir * pushDist;

                    TryPushPlayer(p1, pushVector);

                    TryPushPlayer(p2, -pushVector);
                }
            }
        }

        if (_activeEncounterIndex != -1)
        {
            ServerEncounter active = _allEncounters.Find(e => e.Id == _activeEncounterIndex);
            if (active != null && !active.IsCompleted)
            {
                Bounds b = active.Setup.TriggerBounds.bounds;
                foreach (var p in _serverPlayers)
                {
                    // Принудительно возвращаем всех, кто мог вылететь, внутрь арены
                    p.Position.x = Mathf.Clamp(p.Position.x, b.min.x + playerRadius, b.max.x - playerRadius);
                    p.Position.z = Mathf.Clamp(p.Position.z, b.min.z + playerRadius, b.max.z - playerRadius);
                }
            }
        }
        
        UpdatePlayerProjectiles();
        UpdateBossProjectiles();

        if (_serverPlayers.Count > 0 && _serverPlayers.TrueForAll(p => p.IsDead)) SendGameOver("Все погибли!");

        BroadcastGameState();
    }

    /// <summary>
    /// Активирует энкаунтер
    /// </summary>
    private void ActivateEncounter(ServerEncounter enc, Vector3 initiatorPos)
    {
        _activeEncounterIndex = enc.Id;
        enc.IsActive = true;
        foreach (var p in _serverPlayers)
        {
            if (!enc.Setup.TriggerBounds.bounds.Contains(p.Position)) p.Position = initiatorPos;
        }
    }

    /// <summary>
    /// Обновляет состояние загадки с бочкой
    /// </summary>
    private void UpdatePuzzleBehavior(ServerEncounter puzzle)
    {
        if (puzzle.IsCompleted)
        {
            if (puzzle.Setup.TargetZone != null)
                puzzle.Position = puzzle.Setup.TargetZone.position;

            return;
        }

        puzzle.PoisonTimer -= Time.deltaTime;
        if (puzzle.PoisonTimer <= 0)
        {
            puzzle.PoisonTimer = 1.0f;
            foreach (var p in _serverPlayers) if (!p.IsDead) ApplyDamage(p, puzzle.Setup.PoisonDamage);
        }

        if (puzzle.Setup.TargetZone != null)
        {
            if (Vector3.Distance(puzzle.Position, puzzle.Setup.TargetZone.position) < 1.5f)
            {
                puzzle.Position = puzzle.Setup.TargetZone.position;

                CompleteActiveEncounter(puzzle);
            }
        }
    }

    /// <summary>
    /// Обновляет состояние текущего босса
    /// </summary>
    private void UpdateBossBehavior(ServerEncounter boss)
    {
        if (boss.CurrentHp <= 0)
        {
            CompleteActiveEncounter(boss); 
            return;
        }

        ServerPlayer target = null;
        float minDist = float.MaxValue;
        foreach (var p in _serverPlayers)
        {
            if (p.IsDead) continue;
            float d = Vector3.Distance(boss.Position, p.Position);
            if (d < minDist) { minDist = d; target = p; }
        }

        if (target != null)
        {
            Vector3 dir = (target.Position - boss.Position).normalized;

            boss.Position += dir * boss.MoveSpeed * Time.deltaTime; 

            boss.RotationY = Quaternion.LookRotation(dir).eulerAngles.y;

            if (minDist < 2.0f)
            {
                ApplyDamage(target, Balance.BossContactDamage);
                target.Position += dir * Balance.BossKnockbackForce;
            }
        }

        boss.ShootTimer -= Time.deltaTime;
        if (boss.ShootTimer <= 0)
        {
            boss.ShootTimer = boss.ShootInterval; 

            int count = boss.Setup.ProjectileCount > 0 ? boss.Setup.ProjectileCount : Balance.BossProjectilesCount;
            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * step * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                _bossProjectiles.Add(new ServerBossProjectile { Position = boss.Position + Vector3.up, Direction = dir });
            }
        }
    }

    /// <summary>
    /// Обновляет состояния пуль игроков
    /// </summary>
    private void UpdatePlayerProjectiles()
    {
        ServerEncounter active = (_activeEncounterIndex != -1) ? _allEncounters.Find(b => b.Id == _activeEncounterIndex) : null;

        for (int i = _playerProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = _playerProjectiles[i];
            float stepDist = Balance.ProjectileSpeed * Time.deltaTime;
            Vector3 step = proj.Direction * stepDist;
            bool remove = false;

            if (Physics.SphereCast(
                proj.Position,
                0.25f,
                proj.Direction,
                out RaycastHit hit,
                stepDist,
                LayerMask.GetMask("Obstacle"),
                QueryTriggerInteraction.Ignore))
            {
                remove = true;
            }
            else
            {
                proj.Position += step;
            }
            
            if (!remove && active != null && active.Setup.Type == EncounterType.Boss)
            {
                if (Vector3.Distance(proj.Position, active.Position) < 3.0f + 0.25f)
                {
                    active.CurrentHp -= proj.Damage;
                    remove = true;
                }
            }
            if (remove || proj.Position.magnitude > 200) _playerProjectiles.RemoveAt(i);
        }
    }

    /// <summary>
    /// Обновляет состояние пуль босса
    /// </summary>
    private void UpdateBossProjectiles()
    {
        ServerEncounter active = (_activeEncounterIndex != -1) ? _allEncounters.Find(b => b.Id == _activeEncounterIndex) : null;

        for (int i = _bossProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = _bossProjectiles[i];
            float stepDist = Balance.BossProjectileSpeed * Time.deltaTime;
            Vector3 step = proj.Direction * stepDist;
            bool remove = false;

            if (Physics.SphereCast(
                proj.Position,
                0.25f,
                proj.Direction,
                out RaycastHit hit,
                stepDist,
                LayerMask.GetMask("Obstacle"),
                QueryTriggerInteraction.Ignore))
            {
                remove = true;
            }
            else
            {
                proj.Position += step;
            }

            if (!remove)
            {
                foreach (var p in _serverPlayers)
                {
                    if (p.IsDead) continue;

                    if (Vector3.Distance(proj.Position, p.Position) < 1.2f)
                    {
                        ApplyDamage(p, Balance.BossProjectileDamage);
                        remove = true; break;
                    }
                }
            }

            if (active != null && !active.Setup.TriggerBounds.bounds.Contains(proj.Position)) remove = true;
            if (remove) _bossProjectiles.RemoveAt(i);
        }
    }

    /// <summary>
    ///  Обрабатываети действия на сервере
    /// </summary>
    private void ServerHandleAction(ServerPlayer p, int action, int targetId)
    {
        if (p.IsDead) return;
        ServerEncounter active = (_activeEncounterIndex != -1) ? _allEncounters.Find(b => b.Id == _activeEncounterIndex) : null;

        if (action == 1 && p.MeleeTimer <= 0) 
        {
            p.MeleeTimer = Balance.MeleeCooldown;

            if (active != null)
            {
                if (active.Setup.Type == EncounterType.Boss)
                {
                    if (Vector3.Distance(p.Position, active.Position) < Balance.MeleeRadius + 3f)
                    {
                        Vector3 dir = active.Position - p.Position;
                        if (Vector3.Angle(p.Rotation * Vector3.forward, dir) < Balance.MeleeAngle / 2)
                            active.CurrentHp -= p.MeleeDmg;
                    }
                }

                else if (active.Setup.Type == EncounterType.Puzzle_Soccer)
                {
                    if (active.IsCompleted) return;
                    
                    float hitCheckDist = Balance.MeleeRadius + Balance.PuzzleCubeRadius + 0.5f;

                    if (Vector3.Distance(p.Position, active.Position) < hitCheckDist)
                    {
                        Vector3 dirToBall = active.Position - p.Position;
                        dirToBall.y = 0;

                        if (Vector3.Angle(p.Rotation * Vector3.forward, dirToBall) < Balance.MeleeAngle / 2)
                        {
                            Vector3 pushDir = dirToBall.normalized;
                            float totalForce = Balance.PuzzleCubePushForce;

                            Vector3 origin = active.Position;
                            origin.y = Balance.PuzzleCubeRadius;

                            if (Physics.SphereCast(origin, Balance.PuzzleCubeRadius, pushDir, out RaycastHit hit, totalForce, LayerMask.GetMask("Obstacle"), QueryTriggerInteraction.Ignore))
                            {
                                float distToWall = Mathf.Max(0, hit.distance - 0.05f); 
                                Vector3 posAtWall = active.Position + pushDir * distToWall;

                                Vector3 reflectDir = Vector3.Reflect(pushDir, hit.normal);
                                reflectDir.y = 0;
                                reflectDir.Normalize();

                                float remainingDist = (totalForce - distToWall) * 0.8f; 

                                Vector3 originAtWall = posAtWall;
                                originAtWall.y = Balance.PuzzleCubeRadius;

                                if (Physics.SphereCast(originAtWall, Balance.PuzzleCubeRadius, reflectDir, out RaycastHit hit2, remainingDist, LayerMask.GetMask("Obstacle"), QueryTriggerInteraction.Ignore))
                                {
                                    active.Position = posAtWall;
                                }
                                else
                                {
                                    active.Position = posAtWall + reflectDir * remainingDist;
                                }
                            }
                            else
                            {
                                active.Position += pushDir * totalForce;
                            }

                            active.Position.y = Balance.PuzzleCubeRadius; 
                        }
                    }
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Colors)
                {
                    if (active.PuzzlePhase == 1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (i >= active.Setup.PuzzlePoints.Count) break;

                            Transform pillar = active.Setup.PuzzlePoints[i];

                            if (Vector3.Distance(p.Position, pillar.position) < Balance.MeleeRadius + 1.0f)
                            {
                                if (Vector3.Angle(p.Rotation * Vector3.forward, pillar.position - p.Position) < 60)
                                {
                                    int expectedColorIndex = active.ColorSequence[active.CurrentStep];

                                    if (i == expectedColorIndex)
                                    {
                                        active.CurrentStep++;
                                        
                                        if (active.CurrentStep >= active.ColorSequence.Count)
                                        {
                                            active.CurrentStreak++;
                                            active.ColorSequence.Clear(); 
                                            active.PuzzlePhase = 0; 

                                            if (active.CurrentStreak >= 2)
                                            {
                                                CompleteActiveEncounter(active); 
                                            }
                                            else
                                            {
                                                active.PuzzlePhase = 3;
                                                active.PuzzleTimer = Balance.ColorRoundDelay;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        active.CurrentStreak = 0; 
                                        active.PuzzlePhase = 2;   
                                        active.PuzzleTimer = 2.0f; 

                                        foreach (var victim in _serverPlayers)
                                            ApplyDamage(victim, Balance.ColorDamageOnFail);
                                    }
                                    return; 
                                }
                            }
                        }
                    }
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Laser)
                {
                    if (_visualLaserPuzzle)
                    {
                        _visualLaserPuzzle.transform.position = active.Position;

                        Vector3[] platePositions = new Vector3[active.Setup.PuzzlePoints.Count];
                        for (int i = 0; i < active.Setup.PuzzlePoints.Count; i++)
                            platePositions[i] = active.Setup.PuzzlePoints[i].position;

                        _visualLaserPuzzle.UpdateState(
                            active.LaserAngles.ToArray(),
                            active.PlatesProgress,
                            active.PlatesActiveState,
                            platePositions
                        );
                    }

                    if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                    if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
                    if (_visualColorPuzzle) _visualColorPuzzle.gameObject.SetActive(false);
                }
            }

          for (int i = _bossProjectiles.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(p.Position, _bossProjectiles[i].Position) < Balance.MeleeRadius)
                    _bossProjectiles.RemoveAt(i);
            }
        }
        else if (action == 2 && p.RangeTimer <= 0) 
        {
            p.RangeTimer = p.RangeFireRate;

            Vector3 spawnPos = p.Position;
            spawnPos.y = 0.5f;

            _playerProjectiles.Add(new ServerProjectile
            {
                Position = spawnPos + p.Rotation * Vector3.forward * 0.5f,
                Direction = p.Rotation * Vector3.forward,
                Damage = p.RangeDmg,
                OwnerSlot = p.Info.SlotIndex
            });
        }
        else if (action == 3 && p.HealTimer <= 0)
        {
            var target = _serverPlayers.Find(x => x.Info.SlotIndex == targetId);
            if (target != null && Vector3.Distance(p.Position, target.Position) < Balance.HealDistance)
            {
                p.HealTimer = Balance.HealCooldown;
                target.IsDead = false; target.CurrentHp += p.HealAmount;
                if (target.CurrentHp > target.MaxHp) target.CurrentHp = target.MaxHp;
            }
        }
    }

    /// <summary>
    /// Бродкастит всем состояние игры на сервере
    /// </summary>
    private void BroadcastGameState()
    {
        byte[] data;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)PacketType.GameState);
            
            w.Write(_serverPlayers.Count);
            foreach (var p in _serverPlayers)
            {
                w.Write(p.Info.SlotIndex); w.Write(p.Position.x); w.Write(p.Position.z);
                w.Write(p.Rotation.eulerAngles.y); w.Write(p.CurrentHp); w.Write(p.MaxHp); w.Write(p.IsDead);
                w.Write(p.MeleeTimer); w.Write(p.RangeTimer); w.Write(p.HealTimer);
            }
            
            w.Write(_playerProjectiles.Count);
            foreach (var pr in _playerProjectiles) { w.Write(pr.Position.x); w.Write(pr.Position.z); }

            w.Write(_activeEncounterIndex);
            if (_activeEncounterIndex != -1)
            {
                ServerEncounter active = _allEncounters.Find(b => b.Id == _activeEncounterIndex);
                if (active != null)
                {
                    w.Write((int)active.Setup.Type);
                    w.Write(active.IsCompleted);
                    w.Write(active.Position.x); w.Write(active.Position.z);
                    w.Write(active.RotationY);

                    if (active.Setup.Type == EncounterType.Boss)
                    {
                        w.Write(active.CurrentHp); w.Write(active.MaxHp);
                    }
                    else if (active.Setup.Type == EncounterType.Puzzle_Colors)
                    {
                        w.Write(active.PuzzlePhase);
                        w.Write(active.CurrentStreak);
                        w.Write(active.PuzzleTimer);

                        int showIndex = -1;

                        if (active.PuzzlePhase == 0 && active.ColorSequence.Count > 0)
                        {
                            int seqIndex = Mathf.FloorToInt(active.PuzzleTimer / Balance.ColorShowInterval);
                            if (seqIndex < active.ColorSequence.Count)
                                showIndex = active.ColorSequence[seqIndex];
                        }

                        else if (active.PuzzlePhase == 1 && active.CurrentStep > 0)
                        {
                            showIndex = active.ColorSequence[active.CurrentStep - 1];
                        }

                        w.Write(showIndex);
                    }
                    else if (active.Setup.Type == EncounterType.Puzzle_Laser)
                    {
                        w.Write(active.LaserAngles.Count);
                        foreach (float angle in active.LaserAngles) w.Write(angle);

                        w.Write(active.PlatesProgress.Length);
                        for (int i = 0; i < active.PlatesProgress.Length; i++)
                        {
                            w.Write(active.PlatesProgress[i]);
                            w.Write(active.PlatesActiveState[i]); 
                        }
                    }
                }
            }

            w.Write(_bossProjectiles.Count);
            foreach (var bp in _bossProjectiles) { w.Write(bp.Position.x); w.Write(bp.Position.z); }

            data = ms.ToArray();
        }

        foreach (var p in _serverPlayers)
        {
            if (p.Info.IpEndpointStr == "Host") continue;
            IPEndPoint target = ParseEndPoint(p.Info.IpEndpointStr);
            if (target != null) NetworkManager.Instance.SendPacketTo(data, target);
        }
    }

    /// <summary>
    /// Обрабатывает входящие данные
    /// </summary>
    private void HandleData(byte[] data, IPEndPoint sender)
    {
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader r = new BinaryReader(ms))
        {
            PacketType type = (PacketType)r.ReadByte();

            if (NetworkManager.Instance.IsServer)
            {
                if (type == PacketType.GameInput)
                {
                    string id = (sender == null) ? "Host" : sender.ToString();
                    ServerPlayer p = _serverPlayers.Find(x => x.Info.IpEndpointStr == id);
                    if (p == null && sender == null) p = _serverPlayers.Find(x => x.Info.IpEndpointStr == "Host");
                    if (p != null)
                    {
                        p.LastPacketTime = System.DateTime.Now;
                        p.InputMove = new Vector3(r.ReadSingle(), 0, r.ReadSingle());
                        p.InputLook = new Vector3(r.ReadSingle(), 0, r.ReadSingle());
                        byte act = r.ReadByte(); int trg = r.ReadInt32();
                        if (act > 0) ServerHandleAction(p, act, trg);
                    }
                }
                else if (type == PacketType.ClientSceneLoaded)
                {
                    if (_isGameRunning && sender != null) NetworkManager.Instance.SendPacketTo(new byte[] { (byte)PacketType.MatchStart }, sender);
                }
            }
            else
            {
                if (type == PacketType.MatchStart) 
                { 
                    if (!_isGameRunning) 
                    { 
                        _isGameRunning = true; 
                        UI.HideWaitingPanel();
                    } 
                }
                else if (type == PacketType.ServerShutdown)
                {
                    // Читаем причину ("VICTORY" или другое)
                    string reason = r.ReadString();
                    // Выходим в меню с этой причиной
                    ForceDisconnect(reason);
                }
                else if (type == PacketType.GameState)
                {
                    _lastServerHeartbeat = System.DateTime.Now;
                    HashSet<int> activeSlots = new HashSet<int>();

                    int pCount = r.ReadInt32();
                    for (int i = 0; i < pCount; i++)
                    {
                        int slot = r.ReadInt32(); activeSlots.Add(slot);
                        Vector3 pos = new Vector3(r.ReadSingle(), 0, r.ReadSingle());
                        float rotY = r.ReadSingle();
                        int hp = r.ReadInt32(); int max = r.ReadInt32(); bool dead = r.ReadBoolean();
                        float t1 = r.ReadSingle(); float t2 = r.ReadSingle(); float t3 = r.ReadSingle();

                        if (_visualPlayers.ContainsKey(slot))
                        {
                            var vp = _visualPlayers[slot];
                            vp.UpdateState(pos, Quaternion.Euler(0, rotY, 0), hp, max, dead);
                            vp.UpdateMeleeTimer(t1, Balance.MeleeAnimDuration);

                            if (NetworkManager.Instance.LocalPlayerName == NetworkManager.Instance.FinalPlayerList[slot].Name)
                            {
                                var myInfo = NetworkManager.Instance.FinalPlayerList[slot];
                                float maxRangeCd = Balance.RangeBaseFireRate - (myInfo.GearLevels[1] * Balance.RangeFireRateReductionPerLevel);

                                float normMelee = t1 / Balance.MeleeCooldown;
                                float normRange = t2 / maxRangeCd;
                                float normHeal = t3 / Balance.HealCooldown;
                                UI.UpdateCooldowns(normMelee, normRange, normHeal);
                            }
                        }
                    }
                    List<int> toRemove = new List<int>(); foreach (var k in _visualPlayers.Keys) if (!activeSlots.Contains(k)) toRemove.Add(k);
                    foreach (var k in toRemove) { Destroy(_visualPlayers[k].gameObject); _visualPlayers.Remove(k); }

                    foreach (var b in _visualProjectiles) Destroy(b); _visualProjectiles.Clear();
                    int bCount = r.ReadInt32();
                    for (int i = 0; i < bCount; i++) { var b = Instantiate(BulletPrefab, new Vector3(r.ReadSingle(), 0.5f, r.ReadSingle()), Quaternion.identity); _visualProjectiles.Add(b); }

                    int activeId = r.ReadInt32();
                    if (activeId != -1)
                    {
                        int typeENCOUNTER = r.ReadInt32();
                        bool isCompleted = r.ReadBoolean(); 

                        Vector3 pos = new Vector3(r.ReadSingle(), 0, r.ReadSingle());
                        float rot = r.ReadSingle();

                        if (typeENCOUNTER == (int)EncounterType.Boss)
                        {
                            int hp = r.ReadInt32(); int max = r.ReadInt32();

                            if (_visualBoss) _visualBoss.UpdateState(pos, rot, hp, max, true);
                            if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
                        }
                        else if (typeENCOUNTER == (int)EncounterType.Puzzle_Soccer)
                        {
                            if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(pos, true, isCompleted);
                            if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                        }
                        else if (typeENCOUNTER == (int)EncounterType.Puzzle_Colors)
                        {
                            int phase = r.ReadInt32(); 
                            int streak = r.ReadInt32();
                            float timer = r.ReadSingle();
                            int activeIdx = r.ReadInt32();

                            if (_visualColorPuzzle)
                            {
                                _visualColorPuzzle.transform.position = pos;
                                
                                _visualColorPuzzle.UpdateState(timer, streak, activeIdx, phase);
                            }

                            if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                            if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
                        }
                        else if (typeENCOUNTER == (int)EncounterType.Puzzle_Laser)
                        {
                            int lCount = r.ReadInt32();
                            float[] angles = new float[lCount];
                            for (int i = 0; i < lCount; i++) angles[i] = r.ReadSingle();

                            int paCount = r.ReadInt32();
                            float[] progresses = new float[paCount];
                            bool[] states = new bool[paCount];
                            for (int i = 0; i < paCount; i++)
                            {
                                progresses[i] = r.ReadSingle();
                                states[i] = r.ReadBoolean();
                            }

                            if (_visualLaserPuzzle)
                            {
                                _visualLaserPuzzle.transform.position = pos;

                                EncounterSetup localSetup = Encounters.Find(e => e.Type == EncounterType.Puzzle_Laser);

                                Vector3[] platePositions = null;
                                if (activeId >= 0 && activeId < Encounters.Count)
                                {
                                    var pts = Encounters[activeId].PuzzlePoints;
                                    platePositions = new Vector3[pts.Count];
                                    for (int i = 0; i < pts.Count; i++) platePositions[i] = pts[i].position;
                                }

                                _visualLaserPuzzle.UpdateState(angles, progresses, states, platePositions);
                            }
                        }
                    }
                    else
                    {
                        if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                        if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);

                        if (_visualColorPuzzle) _visualColorPuzzle.gameObject.SetActive(false); 
                        if (_visualLaserPuzzle) _visualLaserPuzzle.gameObject.SetActive(false);
                    }

                    foreach (var b in _visualBossBullets) Destroy(b); _visualBossBullets.Clear();
                    int bpCount = r.ReadInt32();
                    for (int i = 0; i < bpCount; i++) { var b = Instantiate(BossBulletPrefab, new Vector3(r.ReadSingle(), 0.5f, r.ReadSingle()), Quaternion.identity); _visualBossBullets.Add(b); }
                }
            }
        }
    }

    /// <summary>
    /// Синхронизирует сервер и UI хоста
    /// </summary>
    private void SyncVisualsHost()
    {
        HashSet<int> activeSlots = new HashSet<int>();

        foreach (var sp in _serverPlayers)
        {
            activeSlots.Add(sp.Info.SlotIndex);
            if (_visualPlayers.ContainsKey(sp.Info.SlotIndex))
            {
                var vp = _visualPlayers[sp.Info.SlotIndex];
                vp.UpdateState(sp.Position, sp.Rotation, sp.CurrentHp, sp.MaxHp, sp.IsDead);
                if (sp.Info.IpEndpointStr == "Host")
                {
                    float maxRangeCd = Balance.RangeBaseFireRate - (sp.Info.GearLevels[1] * Balance.RangeFireRateReductionPerLevel);
                    float normMelee = sp.MeleeTimer / Balance.MeleeCooldown;
                    float normRange = sp.RangeTimer / maxRangeCd;
                    float normHeal = sp.HealTimer / Balance.HealCooldown;
                    UI.UpdateCooldowns(normMelee, normRange, normHeal);
                }
                vp.UpdateMeleeTimer(sp.MeleeTimer, Balance.MeleeAnimDuration);
            }
        }
        List<int> toRemove = new List<int>(); foreach (var key in _visualPlayers.Keys) if (!activeSlots.Contains(key)) toRemove.Add(key);
        foreach (var key in toRemove) { Destroy(_visualPlayers[key].gameObject); _visualPlayers.Remove(key); }
    
        foreach (var go in _visualProjectiles) Destroy(go); _visualProjectiles.Clear();
        foreach (var sp in _playerProjectiles) { var b = Instantiate(BulletPrefab, sp.Position, Quaternion.identity); _visualProjectiles.Add(b); }

        if (_activeEncounterIndex != -1)
        {
            ServerEncounter active = _allEncounters.Find(b => b.Id == _activeEncounterIndex);
            if (active != null)
            {
                if (active.Setup.Type == EncounterType.Boss)
                {
                    if (_visualBoss) _visualBoss.UpdateState(active.Position, active.RotationY, active.CurrentHp, active.MaxHp, true);
                    if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
                    if (_visualColorPuzzle) _visualColorPuzzle.gameObject.SetActive(false);
                    if (_visualLaserPuzzle) _visualLaserPuzzle.gameObject.SetActive(false);
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Soccer)
                {
                    if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(active.Position, true, active.IsCompleted);
                    if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                    if (_visualColorPuzzle) _visualColorPuzzle.gameObject.SetActive(false);
                    if (_visualLaserPuzzle) _visualLaserPuzzle.gameObject.SetActive(false);
                }
                else if (active.Setup.Type == EncounterType.Puzzle_Colors)
                {
                    int showIndex = -1;
                    if (active.PuzzlePhase == 0 && active.ColorSequence.Count > 0)
                    {
                        int seqIndex = Mathf.FloorToInt(active.PuzzleTimer / Balance.ColorShowInterval);
                        if (seqIndex >= 0 && seqIndex < active.ColorSequence.Count) showIndex = active.ColorSequence[seqIndex];
                    }
                    else if (active.PuzzlePhase == 1 && active.CurrentStep > 0) showIndex = active.ColorSequence[active.CurrentStep - 1];

                    if (_visualColorPuzzle)
                    {
                        _visualColorPuzzle.transform.position = active.Position;
                        _visualColorPuzzle.UpdateState(active.PuzzleTimer, active.CurrentStreak, showIndex, active.PuzzlePhase);
                    }
                    if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                    if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
                    if (_visualLaserPuzzle) _visualLaserPuzzle.gameObject.SetActive(false);
                }
                
                else if (active.Setup.Type == EncounterType.Puzzle_Laser)
                {
                    if (_visualLaserPuzzle)
                    {
                        _visualLaserPuzzle.transform.position = active.Position;

                        Vector3[] platePositions = new Vector3[active.Setup.PuzzlePoints.Count];
                        for (int i = 0; i < active.Setup.PuzzlePoints.Count; i++)
                            platePositions[i] = active.Setup.PuzzlePoints[i].position;

                        _visualLaserPuzzle.UpdateState(
                            active.LaserAngles.ToArray(),
                            active.PlatesProgress,
                            active.PlatesActiveState,
                            platePositions
                        );
                    }
                    
                    if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
                    if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
                    if (_visualColorPuzzle) _visualColorPuzzle.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // СБРОС ВСЕХ
            if (_visualBoss) _visualBoss.UpdateState(Vector3.zero, 0, 0, 0, false);
            if (_visualPuzzleBall) _visualPuzzleBall.UpdateState(Vector3.zero, false, false);
            if (_visualColorPuzzle) _visualColorPuzzle.gameObject.SetActive(false);
            if (_visualLaserPuzzle) _visualLaserPuzzle.gameObject.SetActive(false);
        }

        foreach (var b in _visualBossBullets) Destroy(b); _visualBossBullets.Clear();
        foreach (var bp in _bossProjectiles) { var b = Instantiate(BossBulletPrefab, bp.Position, Quaternion.identity); _visualBossBullets.Add(b); }
    }

    /// <summary>
    /// Отправляет сообщение о начале матча на клиент
    /// </summary>
    private void SendMatchStartPacket()
    {
        byte[] data = new byte[] { (byte)PacketType.MatchStart };
        foreach (var p in _serverPlayers)
        {
            if (p.Info.IpEndpointStr == "Host") continue;
            IPEndPoint target = ParseEndPoint(p.Info.IpEndpointStr);
            if (target != null) NetworkManager.Instance.SendPacketTo(data, target);
        }
    }
    private void SendGameOver(string reason)
    {
        _isGameRunning = false;

        StartCoroutine(GameOverRoutine(reason));
    }
    private System.Collections.IEnumerator GameOverRoutine(string reason)
    {
        Debug.Log($"Game Over: {reason}. Sending shutdown packets...");

        // Записываем причину в NetworkManager для Хоста
        NetworkManager.Instance.DisconnectReason = reason;

        // --- ИСПРАВЛЕНИЕ: Формируем пакет с текстом ---
        byte[] data;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)PacketType.ServerShutdown);
            w.Write(reason); // Записываем строку "VICTORY" или причину смерти
            data = ms.ToArray();
        }
        // ----------------------------------------------

        // Отправляем 5 раз для надежности
        for (int i = 0; i < 5; i++)
        {
            foreach (var p in _serverPlayers)
            {
                if (p.Info.IpEndpointStr == "Host") continue;

                IPEndPoint target = ParseEndPoint(p.Info.IpEndpointStr);
                if (target != null)
                {
                    NetworkManager.Instance.SendPacketTo(data, target);
                }
            }
            yield return new WaitForSeconds(0.1f);
        }

        NetworkManager.Instance.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
    }

    /// <summary>
    /// Cоздаёт игрока на клиенте
    /// </summary>
    private void SpawnVisualPlayer(int slotIndex, PlayerInfo info)
    {
        var go = Instantiate(PlayerPrefab, new Vector3((slotIndex - 1.5f) * 3, 0, 0), Quaternion.identity);
        go.Initialize(_colors[slotIndex]);
        if (!_visualPlayers.ContainsKey(slotIndex)) { _visualPlayers.Add(slotIndex, go); }
        if (info.Name == NetworkManager.Instance.LocalPlayerName)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var followScript = cam.GetComponent<CameraFollow>();
                if (followScript != null)
                {
                    followScript.Target = go.transform;
                    followScript.Offset = new Vector3(0, 15, -5);
                }
            }
        }
    }
    private void ForceDisconnect(string reason)
    {
        NetworkManager.Instance.DisconnectReason = reason;
        NetworkManager.Instance.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
    }
    private IPEndPoint ParseEndPoint(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        string[] parts = s.Split(':');
        if (parts.Length < 2) return null;
        return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
    }

    /// <summary>
    /// Обрабатывает движения/нажатия игроков
    /// </summary>
    private void HandleLocalInput()
    {
        if (UI.IsPaused || Camera.main == null) return;
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move.z = 1;
        if (Input.GetKey(KeyCode.S)) move.z = -1;
        if (Input.GetKey(KeyCode.A)) move.x = -1;
        if (Input.GetKey(KeyCode.D)) move.x = 1;
        move.Normalize();
        Vector3 look = Vector3.zero;
        int targetSlotId = -1;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            look = hit.point;
            var vis = hit.collider.GetComponentInParent<PlayerVisualController>();
            if (vis != null)
            {
                foreach (var kvp in _visualPlayers)
                {
                    if (kvp.Value == vis) { targetSlotId = kvp.Key; break; }
                }
            }
        }
        byte action = 0;
        if (Input.GetMouseButtonDown(0)) action = 1;
        if (Input.GetMouseButtonDown(1)) action = 2;
        if (Input.GetKeyDown(KeyCode.Q)) action = 3;
        if (NetworkManager.Instance.IsServer)
        {
            var hostPlayer = _serverPlayers.Find(p => p.Info.IpEndpointStr == "Host");
            if (hostPlayer != null)
            {
                hostPlayer.InputMove = move; hostPlayer.InputLook = look;
                if (action > 0) ServerHandleAction(hostPlayer, action, targetSlotId);
            }
        }
        else
        {
            using (MemoryStream ms = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write((byte)PacketType.GameInput); w.Write(move.x); w.Write(move.z); w.Write(look.x); w.Write(look.z); w.Write(action); w.Write(targetSlotId);
                NetworkManager.Instance.SendPacket(ms.ToArray());
            }
        }
    }
    private void ApplyDamage(ServerPlayer target, int dmg)
    {
        target.CurrentHp -= dmg;
        if (target.CurrentHp <= 0) { target.CurrentHp = 0; target.IsDead = true; }
    }
    public void OnExitButtonPress()
    {
        if (NetworkManager.Instance.IsServer)
        {
            SendGameOver("Хост покинул игру.");
        }
        else
        {
            NetworkManager.Instance.Shutdown();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
        }
    }

    /// <summary>
    /// Обновляет состояние загадки с цветами
    /// </summary>
    private void UpdateColorPuzzleBehavior(ServerEncounter puzzle)
    {
        if (puzzle.IsCompleted) return;

        if (puzzle.ColorSequence.Count == 0)
        {
            int length = Balance.ColorBaseSequenceLength + (puzzle.CurrentStreak / 2);
            for (int i = 0; i < length; i++)
            {
                int newColor;
                do
                {
                    newColor = Random.Range(0, 4);
                }
                while (puzzle.ColorSequence.Count > 0 && newColor == puzzle.ColorSequence[puzzle.ColorSequence.Count - 1]);

                puzzle.ColorSequence.Add(newColor);
            }
            puzzle.PuzzlePhase = 0;
            puzzle.PuzzleTimer = 0;
            puzzle.CurrentStep = 0;
        }

        if (puzzle.PuzzlePhase == 0)
        {
            puzzle.PuzzleTimer += Time.deltaTime;

            float timePerColor = Balance.ColorShowInterval;
            float totalTime = puzzle.ColorSequence.Count * timePerColor;

            if (puzzle.PuzzleTimer >= totalTime + 0.5f) 
            {
                puzzle.PuzzlePhase = 1; 
                puzzle.PuzzleTimer = 0; 
            }
        }
        else if (puzzle.PuzzlePhase == 2)
        {
            puzzle.PuzzleTimer -= Time.deltaTime;
            if (puzzle.PuzzleTimer <= 0)
            {
                puzzle.ColorSequence.Clear();
                puzzle.PuzzlePhase = 0;
            }
        }
        else if (puzzle.PuzzlePhase == 3)
        {
            puzzle.PuzzleTimer -= Time.deltaTime;
            if (puzzle.PuzzleTimer <= 0)
            {
                puzzle.ColorSequence.Clear(); 
                puzzle.PuzzlePhase = 0;       
                puzzle.PuzzleTimer = 0;
                puzzle.CurrentStep = 0;
            }
        }
    }
    private void UpdateLaserPuzzleBehavior(ServerEncounter puzzle)
    {
        if (puzzle.IsCompleted) return;

        for (int i = 0; i < _serverPlayers.Count; i++)
        {
            var p = _serverPlayers[i];
            if (p.IsDead) continue; 

            Vector3 dirToPlayer = (p.Position - puzzle.Position).normalized;
            float targetAngle = Quaternion.LookRotation(dirToPlayer).eulerAngles.y;

            float currentAngle = puzzle.LaserAngles[i];
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Balance.LaserTrackingSpeed * Time.deltaTime);
            puzzle.LaserAngles[i] = newAngle;

            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(newAngle, targetAngle));

            if (angleDiff < Balance.LaserWidthAngle / 2f && Vector3.Distance(p.Position, puzzle.Position) < Balance.LaserLength)
            {
                if (p.InvulnerabilityTimer <= 0)
                {
                    ApplyDamage(p, Balance.LaserDamage);
                    p.InvulnerabilityTimer = 1.0f; 

                    Vector3 pushDir = Quaternion.Euler(0, newAngle + 90, 0) * Vector3.forward;
                    p.Position += pushDir * 5f * Time.deltaTime; 
                }
            }
        }

        bool allCompleted = true;

        for (int i = 0; i < puzzle.Setup.PuzzlePoints.Count; i++)
        {
            if (puzzle.PlatesProgress[i] >= 100f)
            {
                puzzle.PlatesProgress[i] = 100f;
                puzzle.PlatesActiveState[i] = true; 
                continue;
            }

            allCompleted = false; 
            Transform plateTransform = puzzle.Setup.PuzzlePoints[i];
            bool isStanding = false;

            foreach (var p in _serverPlayers)
            {
                if (!p.IsDead && Vector3.Distance(p.Position, plateTransform.position) < 2.0f)
                {
                    isStanding = true;
                    break;
                }
            }

            puzzle.PlatesActiveState[i] = isStanding;

            if (isStanding)
            {
                puzzle.PlatesProgress[i] += Balance.LaserChargeSpeed * Time.deltaTime;
            }
        }

        if (allCompleted)
        {
            puzzle.IsCompleted = true;
            puzzle.IsActive = false;
            CompleteActiveEncounter(puzzle);
        }
    }

    /// <summary>
    /// Обробатывает успешное завершение энкаунтеров
    /// </summary>
    private void CompleteActiveEncounter(ServerEncounter active)
    {
        active.IsActive = false;
        active.IsCompleted = true;

        if (active.Setup.Type == EncounterType.Boss)
            active.IsDead = true;

        _activeEncounterIndex = -1;

        _bossProjectiles.Clear();

        if (active.Id >= _allEncounters.Count - 1)
        {
            SendGameOver("VICTORY");
        }
    }

    // Метод пытается подвинуть игрока, но останавливает перед стеной
    private void TryPushPlayer(ServerPlayer p, Vector3 pushVector)
    {
        float dist = pushVector.magnitude;
        if (dist < 0.001f) return;

        Vector3 origin = p.Position + Vector3.up * 1.0f;
        
        if (Physics.SphereCast(origin, 0.4f, pushVector.normalized, out RaycastHit hit, dist + 0.1f, LayerMask.GetMask("Obstacle"), QueryTriggerInteraction.Ignore))
        {
            // Если стена, двигаем вплотную к ней
            float safeDist = Mathf.Max(0, hit.distance - 0.01f);
            p.Position += pushVector.normalized * safeDist;
        }
        else
        {
            // Путь чист
            p.Position += pushVector;
        }
    }
}