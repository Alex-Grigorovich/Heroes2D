using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;


public class PlayerCombatSystem : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _MaxSpeed = 5f;
    [SerializeField] private float _acceleration = 50f;
    [SerializeField] private float _deceleration = 50f;
    [SerializeField] private float _velocityPower = .9f;

    [Header("Combat Settings")]
    [SerializeField] private float _attackCooldown = 0.9f;
    [SerializeField] private float _attackDuration = 1f;
    [SerializeField] private int _minDamage = 8;
    [SerializeField] private int _maxDamage = 12;
    [SerializeField] private float _attackRadius = 1.2f;
    [SerializeField] private float _attackAngle = 120f;
    [SerializeField] private LayerMask _enemyLayer = 1;

    [Header("Mouse Targeting Settings")]
    [SerializeField] private float _targetingRadius = 3f;
    [SerializeField] private bool _autoTargetClosestEnemy = true;
    [SerializeField] private float _targetSelectionRadius = 1.5f;
    [SerializeField] private bool _showTargetingReticle = true;
    [SerializeField] private GameObject _targetingReticlePrefab;

    [Header("Diablo 2 Style Mechanics")]
    [SerializeField] private int _attackRating = 100;
    [SerializeField] private float _criticalChance = 0.05f;
    [SerializeField] private float _criticalMultiplier = 2f;
    [SerializeField] private bool _canHitMultipleTargets = true;
    [SerializeField] private int _maxTargetsPerSwing = 3;

    [Header("Attack Visualization")]
    [SerializeField] private GameObject _targetZonePrefab;
    [SerializeField] private float _targetZoneDistance = 0.7f;
    [SerializeField] private float _targetZoneShowDuration = 0.8f;
    [SerializeField] private float _targetZoneRadius = 0.5f;

    [Header("Death Settings")]
    [SerializeField] private bool _disableOnDeath = true;
    [SerializeField] private bool _stopMovementOnDeath = true;


    [Header("Dodge Roll Settings")]
    [SerializeField] private float _rollSpeed = 4f;
    [SerializeField] private float _rollDuration = 1.2f;
    [SerializeField] private float _rollCooldown = 0.8f;
    [SerializeField] private bool _isRollInvincible = true;
    [SerializeField] private float _invincibilityDuration = 0.3f;
    [SerializeField] private float _rollStaminaCost = 20f;
    [SerializeField] private KeyCode _rollKey = KeyCode.Space;
    [SerializeField] private bool _allowMovementDuringRoll = false;

    [Header("Roll Animation")]
    [SerializeField] private string _rollBlendTreeParameter = "IsRolling";   // ← БЕЗ ПРОБЕЛА!
    [SerializeField] private string _rollHorizontalParameter = "RollX";     // ← БЕЗ ПРОБЕЛА!
    [SerializeField] private string _rollVerticalParameter = "RollY";       // ← БЕЗ ПРОБЕЛА!
    [SerializeField] private float _rollAnimationSpeed = 1.5f;



    // Компоненты
    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private Camera _mainCamera;
    private SpriteRenderer _playerSpriteRenderer;
    private Color _originalPlayerColor;

    // Входные данные
    private Vector2 _moveInput;
    private Vector2 _velocity;

    // Состояние боя
    private bool _isAttacking = false;
    private bool _isDealingDamage = false;
    private float _currentAttackCooldown = 0f;
    private Vector2 _attackDirection;

    // Система наведения
    private GameObject _currentTarget;
    private GameObject _targetingReticle;
    private Vector2 _mouseWorldPosition;

    // Визуализация атаки
    private GameObject _targetZoneInstance;
    private SpriteRenderer _targetZoneRenderer;

    // Для предотвращения многократного урона по одной цели
    private HashSet<GameObject> _alreadyDamagedEnemies = new HashSet<GameObject>();

    // Состояние персонажа
    private bool _isDead = false;
    private HealthSystem _healthSystem;
    private bool _movementFrozen = false;

    // События для системы щита
    public event Action OnAttackStarted;
    public event Action OnAttackFinished;



    // События для переката
    public event Action OnRollStarted;
    public event Action OnRollFinished;

    // Состояние переката
    private bool _isRolling = false;
    private bool _canRoll = true;
    private float _currentRollCooldown = 0f;
    private Vector2 _rollDirection;
    private Coroutine _rollCoroutine;
    private bool _isInvincible = false;

    private bool _pendingRollRequest = false;


    [Header("Debug")]
    [SerializeField] private bool _showDebugLogs = true;

    // Свойство для доступа из других систем
    public bool IsAttacking => _isAttacking;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _mainCamera = Camera.main;
        _playerSpriteRenderer = GetComponent<SpriteRenderer>();

        CheckRollAnimationsExist();

        if (_playerSpriteRenderer != null)
        {
            _originalPlayerColor = _playerSpriteRenderer.color;
        }

        // Получаем ссылку на HealthSystem
        _healthSystem = GetComponent<HealthSystem>();
        if (_healthSystem == null)
        {
            _healthSystem = FindObjectOfType<HealthSystem>();
        }

        // Создаем ретиклу наведения
        if (_showTargetingReticle && _targetingReticlePrefab != null)
        {
            _targetingReticle = Instantiate(_targetingReticlePrefab);
            _targetingReticle.SetActive(false);
        }


        _canRoll = true;

        // Настройка аниматора
        if (_animator != null)
        {
            _animator.SetFloat("RollSpeed", _rollAnimationSpeed);
            // Инициализируем параметры переката
            _animator.SetFloat(_rollHorizontalParameter, 0);
            _animator.SetFloat(_rollVerticalParameter, 1); // Направление вверх по умолчанию
            _animator.SetBool(_rollBlendTreeParameter, false);

            if (_showDebugLogs)
                Debug.Log($"✅ Animator initialized: RollX={_rollHorizontalParameter}, RollY={_rollVerticalParameter}, IsRolling={_rollBlendTreeParameter}");
        }
        else
        {
            Debug.LogError("❌ Animator not found! Make sure Animator component is attached to the player.");
        }


    }

    private void Update()
    {
        // ТЕСТ: Проверяем, регистрируется ли нажатие пробела
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"🎮 SPACE pressed in Update() - canRoll: {_canRoll}, cooldown: {_currentRollCooldown:F2}, isRolling: {_isRolling}");
        }

        // Проверяем, заблокировано ли движение
        if (IsMovementBlocked())
        {
            // Сбрасываем ввод движения
            _moveInput = Vector2.zero;

            // Отключаем атаки
            if (_isAttacking)
            {
                EndAttack();
            }

            // Отключаем таргетинг
            if (_targetingReticle != null)
            {
                _targetingReticle.SetActive(false);
            }

            return;
        }

        // Проверяем состояние смерти
        CheckDeathState();

        // Если персонаж мертв или движение заморожено - отключаем управление
        if (_isDead || _movementFrozen)
        {
            DisableMovement();
            return;
        }

        // Сначала обновляем ввод и таргетинг
        UpdateMousePosition();
        UpdateTargeting();
        UpdateCooldowns();

        // Затем обрабатываем перекат
        UpdateRoll();

        // Затем атаку
        HandleAttackInput();

        // И наконец анимацию
        UpdateAnimation();
        UpdateTargetingReticle();

        if (_isDealingDamage && _targetZoneInstance != null)
        {
            CheckTargetZoneDamage();
        }

        // Отладка аниматора (можно включить по клавише)
        if (Input.GetKeyDown(KeyCode.F2))
        {
            DebugAnimatorParameters();
        }
    }




    private bool IsMovementBlocked()
    {
        return HealthSystem.Instance != null &&
               (HealthSystem.Instance.IsDead() ||
                IsMovementFrozenByHealthSystem());
        // Убрал HealthSystem.Instance.IsHurting() из проверки
    }

    private bool IsMovementFrozenByHealthSystem()
    {
        // Проверяем наличие метода BlockMovement в HealthSystem через рефлексию
        var healthSystem = HealthSystem.Instance;
        if (healthSystem != null)
        {
            var blockWASDField = healthSystem.GetType().GetField("blockWASD",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (blockWASDField != null)
            {
                return (bool)blockWASDField.GetValue(healthSystem);
            }
        }
        return false;
    }

    void CheckDeathState()
    {
        if (_healthSystem != null)
        {
            var isDeadProperty = _healthSystem.GetType().GetProperty("IsDead");
            if (isDeadProperty != null)
            {
                _isDead = (bool)isDeadProperty.GetValue(_healthSystem);
            }
            else
            {
                var isDeadMethod = _healthSystem.GetType().GetMethod("IsDead");
                if (isDeadMethod != null)
                {
                    _isDead = (bool)isDeadMethod.Invoke(_healthSystem, null);
                }
                else
                {
                    var currentHealthProperty = _healthSystem.GetType().GetProperty("CurrentHealth");
                    if (currentHealthProperty != null)
                    {
                        float currentHealth = (float)currentHealthProperty.GetValue(_healthSystem);
                        _isDead = currentHealth <= 0;
                    }
                }
            }
        }
    }

    void DisableMovement()
    {
        // Останавливаем движение
        if (_stopMovementOnDeath || _movementFrozen)
        {
            _moveInput = Vector2.zero;
            _velocity = Vector2.zero;
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
        }

        // Отключаем атаки
        if (_isAttacking && _movementFrozen)
        {
            EndAttack();
        }

        // Отключаем таргетинг
        if (_targetingReticle != null && _movementFrozen)
        {
            _targetingReticle.SetActive(false);
        }

        // Уничтожаем зоны атаки
        if (_movementFrozen)
        {
            DestroyTargetZone();
        }

        // Отключаем анимацию движения
        if (_animator != null)
        {
            _animator.SetFloat("Speed", 0f);
        }
    }

    private void CheckTargetZoneDamage()
    {
        if (_targetZoneInstance == null || _isDead || _movementFrozen) return;

        Collider2D[] enemiesInZone = Physics2D.OverlapCircleAll(
            _targetZoneInstance.transform.position,
            _targetZoneRadius,
            _enemyLayer
        );

        int hits = 0;

        foreach (Collider2D enemy in enemiesInZone)
        {
            if (!enemy.CompareTag("Enemy")) continue;

            if (_alreadyDamagedEnemies.Contains(enemy.gameObject)) continue;

            if (CheckHitChance(enemy.gameObject))
            {
                DealSingleDamage(enemy.gameObject);
                _alreadyDamagedEnemies.Add(enemy.gameObject);
                hits++;

                if (hits >= _maxTargetsPerSwing) break;
            }
        }

        if (hits > 0)
        {
            Debug.Log($"🎯 TargetZone hit {hits} enemies with SINGLE damage!");
        }
    }

    private void UpdateMousePosition()
    {
        // ДОБАВЬТЕ _isRolling
        if (_isDead || _movementFrozen || _isRolling) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -_mainCamera.transform.position.z;
        _mouseWorldPosition = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }

    private void UpdateTargeting()
    {
        // ДОБАВЬТЕ _isRolling
        if (_isDead || _movementFrozen || _isRolling) return;

        if (_autoTargetClosestEnemy)
        {
            FindBestTarget();
        }
        else
        {
            _attackDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
        }
    }



    private void FindBestTarget()
    {
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, _targetingRadius, _enemyLayer);

        GameObject bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider2D enemy in nearbyEnemies)
        {
            if (!enemy.CompareTag("Enemy")) continue;

            Vector2 toEnemy = (Vector2)enemy.transform.position - _mouseWorldPosition;
            float distanceToCursor = toEnemy.magnitude;

            float score = distanceToCursor;
            float distanceToPlayer = Vector2.Distance(transform.position, enemy.transform.position);
            score += distanceToPlayer * 0.3f;

            if (score < bestScore && distanceToCursor <= _targetSelectionRadius)
            {
                bestScore = score;
                bestTarget = enemy.gameObject;
            }
        }

        _currentTarget = bestTarget;

        if (_currentTarget != null)
        {
            _attackDirection = ((Vector2)_currentTarget.transform.position - (Vector2)transform.position).normalized;
        }
        else
        {
            _attackDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
        }
    }

    private void UpdateTargetingReticle()
    {
        if (_targetingReticle != null && !_isDead && !_movementFrozen)
        {
            if (_currentTarget != null && _showTargetingReticle)
            {
                _targetingReticle.SetActive(true);
                _targetingReticle.transform.position = _currentTarget.transform.position + Vector3.up * 0.5f;
            }
            else
            {
                _targetingReticle.SetActive(false);
            }
        }
        else if (_targetingReticle != null)
        {
            _targetingReticle.SetActive(false);
        }
    }

    private void UpdateCooldowns()
    {
        if (_isDead || _movementFrozen) return;

        if (_currentAttackCooldown > 0f)
        {
            _currentAttackCooldown -= Time.deltaTime;
        }
    }

    private void HandleAttackInput()
    {
        // ДОБАВЬТЕ _isRolling
        if (_isDead || _movementFrozen || _isRolling) return;

        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            StartAttack();
        }
    }

    private bool CanAttack()
    {
        return _currentAttackCooldown <= 0f && !_isAttacking && !_isDead && !_movementFrozen;
    }

    private void StartAttack()
    {
        if (_isDead || _movementFrozen) return;

        _isAttacking = true;
        _currentAttackCooldown = _attackCooldown;

        OnAttackStarted?.Invoke();
        _alreadyDamagedEnemies.Clear();
        _animator.SetBool("IsAttacking", true);
        CreateTargetZone();
        StartCoroutine(AttackSequence());

        Debug.Log($"🎯 Attack started! Direction: {_attackDirection}");
    }

    private void CreateTargetZone()
    {
        if (_targetZonePrefab != null && !_isDead && !_movementFrozen)
        {
            DestroyTargetZone();

            Vector3 targetZonePosition = transform.position + (Vector3)_attackDirection * _targetZoneDistance;
            _targetZoneInstance = Instantiate(_targetZonePrefab, targetZonePosition, Quaternion.identity);
            _targetZoneRenderer = _targetZoneInstance.GetComponent<SpriteRenderer>();

            if (_targetZoneRenderer != null)
            {
                Debug.Log($"🎯 TargetZone created at position: {targetZonePosition}");
            }
            else
            {
                Debug.LogWarning("❌ TargetZone prefab doesn't have SpriteRenderer component!");
            }
        }
        else if (_targetZonePrefab == null && !_isDead && !_movementFrozen)
        {
            Debug.LogWarning("❌ TargetZone prefab is not assigned!");
        }


        var trigger = _targetZoneInstance.GetComponent<TargetZoneTrigger>();
        if (trigger != null)
        {
            trigger.OnEnemyEnter = (collider) =>
            {
                if (_isDealingDamage && !_alreadyDamagedEnemies.Contains(collider.gameObject))
                {
                    if (CheckHitChance(collider.gameObject))
                    {
                        DealSingleDamage(collider.gameObject);
                        _alreadyDamagedEnemies.Add(collider.gameObject);
                    }
                }
            };
        }
    }

    private void DestroyTargetZone()
    {
        if (_targetZoneInstance != null)
        {
            Destroy(_targetZoneInstance);
            _targetZoneInstance = null;
            _targetZoneRenderer = null;
            Debug.Log("🎯 TargetZone destroyed");
        }
    }

    private IEnumerator AttackSequence()
    {
        if (_isDead || _movementFrozen)
        {
            EndAttack();
            yield break;
        }

        // Фаза подготовки (без урона и без зоны!)
        yield return new WaitForSeconds(_attackDuration * 0.2f);

        if (_isDead || _movementFrozen)
        {
            EndAttack();
            yield break;
        }

        // === НАЧАЛО ФАЗЫ УРОНА ===
        _isDealingDamage = true;
        _alreadyDamagedEnemies.Clear(); // ← КРИТИЧЕСКИ ВАЖНО!
        CreateTargetZone();             // ← Создаём зону ТОЛЬКО ЗДЕСЬ
        Debug.Log("💥 START dealing damage phase");

        // Проверяем врагов КАЖДЫЙ КАДР в течение 0.4 сек
        float damagePhaseTime = 0f;
        float damagePhaseDuration = _attackDuration * 0.4f;

        while (damagePhaseTime < damagePhaseDuration && !_isDead && !_movementFrozen)
        {
            CheckTargetZoneDamage(); // ← Проверяем КАЖДЫЙ КАДР
            damagePhaseTime += Time.deltaTime;
            yield return null;
        }

        // === КОНЕЦ ФАЗЫ УРОНА ===
        _isDealingDamage = false;
        DestroyTargetZone();
        Debug.Log("💥 END dealing damage phase");

        // Фаза завершения
        yield return new WaitForSeconds(_attackDuration * 0.4f);

        if (!_isDead && !_movementFrozen)
        {
            EndAttack();
        }
    }

    private void DealSingleDamage(GameObject enemy)
    {
        if (_isDead || _movementFrozen) return;

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Vector2 directionToEnemy = (enemy.transform.position - transform.position).normalized;

            int finalDamage = CalculateDiabloDamage();
            bool isCritical = CheckCriticalStrike();

            if (isCritical)
            {
                finalDamage = Mathf.RoundToInt(finalDamage * _criticalMultiplier);
                Debug.Log($"💥 TARGET ZONE CRITICAL HIT! {finalDamage} damage to {enemy.name}");
            }
            else
            {
                Debug.Log($"🎯 TARGET ZONE HIT! {finalDamage} damage to {enemy.name}");
            }

            enemyHealth.TakeDamage(finalDamage, directionToEnemy);
            ShowDamageEffect(enemy.transform.position, finalDamage, isCritical);
        }
    }

    // Метод для вызова из системы здоровья
    public void TakeDamage(int damage, Vector2 hitDirection, float knockbackForce = 0f)
    {
        if (_isDead || _movementFrozen || _isInvincible)
        {
            Debug.Log("Персонаж мертв или движение заморожено, урон не принимается");
            return;
        }

        Debug.Log($"💥 Персонаж получает урон: {damage}, направление: {hitDirection}");

        // Наносим урон через HealthSystem
        if (_healthSystem != null)
        {
            var takeDamageMethod = _healthSystem.GetType().GetMethod("TakeDamage");
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(_healthSystem, new object[] { damage });
            }
        }

        // Применяем отбрасывание
        if (knockbackForce > 0f && _rigidbody != null)
        {
            _rigidbody.velocity = Vector2.zero;
            _rigidbody.AddForce(hitDirection * knockbackForce, ForceMode2D.Impulse);
        }
    }

    private bool CheckHitChance(GameObject enemy)
    {
        if (_isDead || _movementFrozen) return false;

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null) return true;

        float baseHitChance = _attackRating / (_attackRating + enemyHealth.GetDefense()) * 100f;
        float finalHitChance = Mathf.Clamp(baseHitChance, 5f, 95f);

        bool hit = UnityEngine.Random.Range(0f, 100f) <= finalHitChance;
        Debug.Log($"🎯 Hit chance vs {enemy.name}: {finalHitChance:F1}% - {(hit ? "HIT" : "MISS")}");

        return hit;
    }

    private int CalculateDiabloDamage()
    {
        if (_isDead || _movementFrozen) return 0;

        int baseDamage = UnityEngine.Random.Range(_minDamage, _maxDamage + 1);
        int finalDamage = baseDamage;
        Debug.Log($"⚔️ Damage roll: {baseDamage} (range: {_minDamage}-{_maxDamage})");
        return finalDamage;
    }

    private bool CheckCriticalStrike()
    {
        if (_isDead || _movementFrozen) return false;

        bool isCritical = UnityEngine.Random.Range(0f, 1f) <= _criticalChance;
        if (isCritical) Debug.Log($"🎲 Critical strike! Chance: {_criticalChance * 100}%");
        return isCritical;
    }

    private void ShowDamageEffect(Vector3 position, int damage, bool isCritical)
    {
        if (_isDead || _movementFrozen) return;

        StartCoroutine(FlashEnemy(position, isCritical));
        CreateDamagePopup(position, damage, isCritical);
    }

    private IEnumerator FlashEnemy(Vector3 position, bool isCritical)
    {
        Collider2D enemy = Physics2D.OverlapPoint(position, _enemyLayer);
        if (enemy != null && enemy.TryGetComponent<SpriteRenderer>(out SpriteRenderer sprite))
        {
            Color originalColor = sprite.color;
            sprite.color = isCritical ? Color.yellow : Color.red;
            yield return new WaitForSeconds(0.2f);
            sprite.color = originalColor;
        }
    }

    private void CreateDamagePopup(Vector3 position, int damage, bool isCritical)
    {
        GameObject popup = new GameObject("DamagePopup");
        popup.transform.position = position + Vector3.up * 0.5f;

        TextMesh textMesh = popup.AddComponent<TextMesh>();
        textMesh.text = damage.ToString();
        textMesh.color = isCritical ? Color.yellow : Color.white;
        textMesh.fontSize = 20;
        textMesh.anchor = TextAnchor.MiddleCenter;

        Destroy(popup, 1f);
        StartCoroutine(AnimateDamagePopup(popup.transform));
    }

    private IEnumerator AnimateDamagePopup(Transform popup)
    {
        float duration = 1f;
        float elapsed = 0f;
        Vector3 startPos = popup.position;
        Vector3 endPos = startPos + Vector3.up * 2f;

        while (elapsed < duration)
        {
            if (popup != null)
            {
                popup.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                elapsed += Time.deltaTime;
            }
            yield return null;
        }
    }

    public void EndAttack()
    {
        if (_isAttacking)
        {
            _isAttacking = false;
            _isDealingDamage = false;

            OnAttackFinished?.Invoke();

            if (_animator != null)
            {
                _animator.SetBool("IsAttacking", false);
            }

            _alreadyDamagedEnemies.Clear();
            ResetPlayerColor();

            Debug.Log("✅ Attack finished");
        }
    }

    private void ResetPlayerColor()
    {
        if (_playerSpriteRenderer != null)
        {
            _playerSpriteRenderer.color = _originalPlayerColor;
        }
    }

    private void UpdateAnimation()
    {
        if (_isDead || _movementFrozen)
        {
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool(_rollBlendTreeParameter, false);
            }
            return;
        }

        // Проверяем, получает ли персонаж урон
        bool isHurting = false;
        if (_healthSystem != null)
        {
            isHurting = _healthSystem.IsHurting();
        }

        // ПРИОРИТЕТ 1: Урон
        if (isHurting)
        {
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsAttacking", false);
                _animator.SetBool(_rollBlendTreeParameter, false);
            }
            return;
        }

        if (_isRolling)
        {
            // ПРИОРИТЕТ: Перекат всегда выше урона
            if (_animator != null)
            {
                _animator.SetFloat(_rollHorizontalParameter, _rollDirection.x);
                _animator.SetFloat(_rollVerticalParameter, _rollDirection.y);
                _animator.SetBool(_rollBlendTreeParameter, true);
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsAttacking", false);
            }
            return; // ← Выходим здесь, даже если isHurting == true
        }


        else if (_isAttacking)
        {
            // Во время атаки - только направление атаки
            if (_animator != null)
            {
                _animator.SetFloat("Horizontal", _attackDirection.x);
                _animator.SetFloat("Vertical", _attackDirection.y);
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsAttacking", true);
            }
        }
        else if (_moveInput.magnitude > 0.1f)
        {
            // Движение
            if (_animator != null)
            {
                _animator.SetFloat("Horizontal", _moveInput.normalized.x);
                _animator.SetFloat("Vertical", _moveInput.normalized.y);
                _animator.SetFloat("Speed", _moveInput.magnitude);
                _animator.SetBool("IsAttacking", false);
                _animator.SetBool(_rollBlendTreeParameter, false); // Гарантируем выключение переката
            }
        }
        else
        {
            // Без движения - направление на мышь
            Vector2 lookDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
            if (lookDirection.magnitude > 0.1f && _animator != null)
            {
                _animator.SetFloat("Horizontal", lookDirection.x);
                _animator.SetFloat("Vertical", lookDirection.y);
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsAttacking", false);
                _animator.SetBool(_rollBlendTreeParameter, false); // Гарантируем выключение переката
            }
        }
    }

    private void FixedUpdate()
    {
        // Проверяем состояние смерти
        if (_isDead || _movementFrozen)
        {
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
            return;
        }

        // ЕСЛИ КАТИМСЯ - ПОЛНОСТЬЮ КОНТРОЛИРУЕМ ДВИЖЕНИЕ ЧЕРЕЗ КОРУТИНУ
        if (_isRolling)
        {
            // Ничего не делаем здесь - движение полностью контролируется в RollSequence()
            return;
        }
        // ЕСЛИ АТАКУЕМ - БЛОКИРУЕМ ДВИЖЕНИЕ
        else if (_isAttacking)
        {
            _velocity = Vector2.zero;
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
        }
        // ОБЫЧНОЕ ДВИЖЕНИЕ
        else
        {
            Move();
        }
    }


    private void Move()
    {
        if (_isDead || _movementFrozen) return;

        Vector2 targetVelocity = _moveInput * _MaxSpeed;
        Vector2 velocityDiff = targetVelocity - _velocity;
        float accelerateRate = (Mathf.Abs(targetVelocity.magnitude) > 0.01f) ? _acceleration : _deceleration;

        Vector2 movement = velocityDiff * (accelerateRate * Time.fixedDeltaTime);

        _velocity += movement;
        _velocity = Vector2.ClampMagnitude(_velocity, _MaxSpeed);
        _velocity *= MathF.Pow(1f - _velocityPower, Time.fixedDeltaTime);

        if (_rigidbody != null)
        {
            _rigidbody.MovePosition(_rigidbody.position + _velocity * Time.fixedDeltaTime);
            _rigidbody.angularVelocity = 0f;
            transform.rotation = Quaternion.identity;
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        // Если катимся - ИГНОРИРУЕМ ввод движения
        if (_isDead || _movementFrozen || !enabled || _isRolling)
        {
            _moveInput = Vector2.zero;
            return;
        }

        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (_isDead || _movementFrozen || !enabled || _isRolling) return;

        if (context.performed && CanAttack())
        {
            StartAttack();
        }
    }

    // Добавляем метод для Input System переката (если используете новый Input System):
    public void OnRoll(InputAction.CallbackContext context)
    {
        if (_isDead || _movementFrozen || !enabled) return;

        if (context.performed && CanRoll())
        {
            StartRoll();
        }
    }

    public void SetDeadState(bool isDead)
    {
        _isDead = isDead;

        if (isDead)
        {
            DisableMovement();
        }
    }

    public void FreezeMovement(bool freeze)
    {
        _movementFrozen = freeze;

        if (freeze)
        {
            _moveInput = Vector2.zero;
            _velocity = Vector2.zero;

            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }

            if (_isAttacking)
            {
                EndAttack();
            }

            if (_isRolling)
            {
                EndRoll();
            }

            Debug.Log("Player movement frozen");
        }
        else
        {
            Debug.Log("Player movement unfrozen");
        }
    }

    private void OnDestroy()
    {
        DestroyTargetZone();
        ResetPlayerColor();
    }

    private void OnDisable()
    {
        DestroyTargetZone();
        ResetPlayerColor();
    }

    // Визуализация в редакторе
    private void OnDrawGizmosSelected()
    {
        if (_isDead || _movementFrozen) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _targetingRadius);

        if (Application.isPlaying && _targetZoneInstance != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_targetZoneInstance.transform.position, _targetZoneRadius);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_mouseWorldPosition, _targetSelectionRadius);
        }

        if (Application.isPlaying && _isAttacking)
        {
            Gizmos.color = Color.red;
            DrawAttackAngleGizmo();
        }

        if (Application.isPlaying && _currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
        }

        if (Application.isPlaying && _targetZoneInstance != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_targetZoneInstance.transform.position, 0.1f);
            Gizmos.DrawLine(transform.position, _targetZoneInstance.transform.position);
        }


        if (Application.isPlaying && _isRolling)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)_rollDirection * 2f);
            Gizmos.DrawWireSphere(transform.position + (Vector3)_rollDirection * 1.5f, 0.2f);
        }
    }

    private void DrawAttackAngleGizmo()
    {
        float halfAngle = _attackAngle * 0.5f * Mathf.Deg2Rad;

        Vector2 leftDir = new Vector2(
            _attackDirection.x * Mathf.Cos(halfAngle) - _attackDirection.y * Mathf.Sin(halfAngle),
            _attackDirection.x * Mathf.Sin(halfAngle) + _attackDirection.y * Mathf.Cos(halfAngle)
        );

        Vector2 rightDir = new Vector2(
            _attackDirection.x * Mathf.Cos(halfAngle) + _attackDirection.y * Mathf.Sin(halfAngle),
            -_attackDirection.x * Mathf.Sin(halfAngle) + _attackDirection.y * Mathf.Cos(halfAngle)
        );

        Gizmos.DrawLine(transform.position, transform.position + (Vector3)leftDir * _attackRadius);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)rightDir * _attackRadius);
        Gizmos.DrawLine(transform.position + (Vector3)leftDir * _attackRadius,
                        transform.position + (Vector3)rightDir * _attackRadius);
    }




    private void UpdateRoll()
    {
        if (_isDead || _movementFrozen || _isAttacking)
            return;

        // 🔑 ГАРАНТИРУЕМ: если кулдаун ≤ 0, то _canRoll = true
        if (_currentRollCooldown <= 0f)
        {
            _canRoll = true;
            _currentRollCooldown = 0f; // нормализуем
        }
        else
        {
            _currentRollCooldown -= Time.deltaTime;
            if (_currentRollCooldown <= 0f)
            {
                _canRoll = true;
                _currentRollCooldown = 0f;
                if (_showDebugLogs)
                    Debug.Log("✅ Roll cooldown finished, canRoll set to true");
            }
        }

        // Обработка ввода
        if (Input.GetKeyDown(_rollKey))
        {
            if (_showDebugLogs)
                Debug.Log($"🎮 Roll key pressed: {_rollKey}");

            if (CanRoll())
            {
                StartRoll();
            }
            else if (_showDebugLogs)
            {
                Debug.Log($"❌ Cannot roll now (CanRoll returned false)");
            }
        }
    }


    private bool CanRoll()
    {
        // Базовые проверки
        if (_isDead || _movementFrozen || _isAttacking || _isRolling)
        {
            if (_showDebugLogs)
                Debug.Log($"❌ CanRoll false: dead={_isDead}, frozen={_movementFrozen}, attacking={_isAttacking}, rolling={_isRolling}");
            return false;
        }

        // Проверяем кулдаун
        if (!_canRoll || _currentRollCooldown > 0f)
        {
            if (_showDebugLogs)
                Debug.Log($"❌ CanRoll false: canRoll={_canRoll}, cooldown={_currentRollCooldown:F2}");
            return false;
        }

        // УБРАТЬ проверку на isHurting - перекат должен быть доступен во время получения урона
        // HealthSystem.Instance.IsHurting() - УБРАТЬ ЭТУ ПРОВЕРКУ

        return true;
    }

    private void StartRoll()
    {
        if (_isDead || _movementFrozen || _isAttacking || !CanRoll())
        {
            return;
        }

        if (_isRolling) return;

        _isRolling = true;
        _canRoll = false;

        Vector2 rawDirection = GetRollDirection();
        _rollDirection = GetRoundedDirection(rawDirection);

        if (_showDebugLogs)
        {
            Debug.Log($"🔄 Roll direction: Raw=({rawDirection.x:F2}, {rawDirection.y:F2}), Rounded=({_rollDirection.x:F2}, {_rollDirection.y:F2})");
        }

        // 🔥 КРИТИЧЕСКАЯ ПРОВЕРКА: существуют ли параметры?
        if (_animator != null)
        {
            bool HasParameter(string paramName)
            {
                foreach (var param in _animator.parameters)
                {
                    if (param.name == paramName)
                        return true;
                }
                return false;
            }

            bool hasIsRolling = HasParameter(_rollBlendTreeParameter);
            bool hasRollX = HasParameter(_rollHorizontalParameter);
            bool hasRollY = HasParameter(_rollVerticalParameter);

            if (!hasIsRolling || !hasRollX || !hasRollY)
            {
                Debug.LogError($"❌ Animator parameters NOT FOUND! IsRolling={hasIsRolling}, RollX={hasRollX}, RollY={hasRollY}\n" +
                              $"Check names in Animator and in script (no trailing spaces!)");
                return;
            }

            // Сброс и установка — теперь будет работать
            _animator.SetBool(_rollBlendTreeParameter, false);
            _animator.SetFloat(_rollHorizontalParameter, 0);
            _animator.SetFloat(_rollVerticalParameter, 0);
            _animator.Update(0);

            _animator.SetFloat(_rollHorizontalParameter, _rollDirection.x);
            _animator.SetFloat(_rollVerticalParameter, _rollDirection.y);
            _animator.SetBool(_rollBlendTreeParameter, true); // ← Теперь сработает!
            _animator.Update(0);
        }

        OnRollStarted?.Invoke();
        if (_isRollInvincible) StartCoroutine(ApplyInvincibility());
        _rollCoroutine = StartCoroutine(RollSequence());
    }


    // Добавьте этот метод для отладки названий направлений:
    private string GetDirectionName(Vector2 direction)
    {
        Vector2 rounded = GetRoundedDirection(direction);

        if (rounded == Vector2.right) return "Right";
        if (rounded == Vector2.left) return "Left";
        if (rounded == Vector2.up) return "Up";
        if (rounded == Vector2.down) return "Down";
        if (rounded == new Vector2(0.7f, 0.7f)) return "Up-Right";
        if (rounded == new Vector2(-0.7f, 0.7f)) return "Up-Left";
        if (rounded == new Vector2(0.7f, -0.7f)) return "Down-Right";
        if (rounded == new Vector2(-0.7f, -0.7f)) return "Down-Left";

        return $"Unknown: ({direction.x:F2}, {direction.y:F2})";
    }


    private Vector2 GetRollDirection()
    {
        Vector2 direction = Vector2.zero;

        // Читаем нажатые клавиши WASD напрямую
        bool up = Input.GetKey(KeyCode.W);
        bool down = Input.GetKey(KeyCode.S);
        bool left = Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.D);

        // Собираем направление
        if (up) direction.y += 1;
        if (down) direction.y -= 1;
        if (left) direction.x -= 1;
        if (right) direction.x += 1;

        // Если нет ввода — используем мышь как fallback
        if (direction == Vector2.zero)
        {
            Vector2 mouseDir = (_mouseWorldPosition - (Vector2)transform.position).normalized;
            if (mouseDir.magnitude > 0.1f)
            {
                direction = mouseDir;
                if (_showDebugLogs)
                    Debug.Log($"🎮 Using mouse direction: ({direction.x:F2}, {direction.y:F2})");
            }
            else
            {
                // Если и мышь неактивна — используем последнее направление атаки или вниз по умолчанию
                if (_attackDirection.magnitude > 0.1f)
                {
                    direction = _attackDirection.normalized;
                }
                else
                {
                    direction = Vector2.down;
                }
            }
        }

        // Нормализуем только если не нулевой
        if (direction.magnitude > 0.1f)
        {
            direction = direction.normalized;
        }

        if (_showDebugLogs)
            Debug.Log($"🎮 Raw roll input from keys: W={Input.GetKey(KeyCode.W)}, A={Input.GetKey(KeyCode.A)}, S={Input.GetKey(KeyCode.S)}, D={Input.GetKey(KeyCode.D)} → Direction: ({direction.x:F2}, {direction.y:F2})");

        return direction;
    }

  

    private void DebugAnimatorParameters()
    {
        if (_animator != null && _showDebugLogs)
        {
            Debug.Log($"🎭 Animator Parameters:");
            Debug.Log($"  - RollX: {_animator.GetFloat(_rollHorizontalParameter):F2}");
            Debug.Log($"  - RollY: {_animator.GetFloat(_rollVerticalParameter):F2}");
            Debug.Log($"  - IsRolling: {_animator.GetBool(_rollBlendTreeParameter)}");
            Debug.Log($"  - Speed: {_animator.GetFloat("Speed"):F2}");
            Debug.Log($"  - Horizontal: {_animator.GetFloat("Horizontal"):F2}");
            Debug.Log($"  - Vertical: {_animator.GetFloat("Vertical"):F2}");

            // Проверяем состояние анимации
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"  - Current state length: {stateInfo.length:F2}, normalized time: {stateInfo.normalizedTime:F2}");
        }
    }

    private void CheckRollAnimationsExist()
    {
        if (_animator == null) return;

        // Получаем контроллер аниматора
        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;

        if (controller == null)
        {
            Debug.LogError("❌ Animator Controller не назначен!");
            return;
        }

        Debug.Log($"📁 Animator Controller: {controller.name}");

        // Проверяем все анимации в контроллере
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip.name.Contains("Roll") || clip.name.Contains("rolling", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"✅ Found roll animation: {clip.name} (length: {clip.length:F2}s)");
            }
        }
    }

    private Vector2 GetRoundedDirection(Vector2 direction)
    {
        if (direction.magnitude < 0.1f)
            return Vector2.down; // По умолчанию вниз

        direction = direction.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle = (angle + 360) % 360;

        // Для 8 направлений (каждые 45 градусов)
        if (angle >= 337.5f || angle < 22.5f)
            return Vector2.right;           // Вправо (1, 0)
        else if (angle >= 22.5f && angle < 67.5f)
            return new Vector2(0.7f, 0.7f);   // Вверх-вправо
        else if (angle >= 67.5f && angle < 112.5f)
            return Vector2.up;               // Вверх (0, 1)
        else if (angle >= 112.5f && angle < 157.5f)
            return new Vector2(-0.7f, 0.7f);  // Вверх-влево
        else if (angle >= 157.5f && angle < 202.5f)
            return Vector2.left;             // Влево (-1, 0)
        else if (angle >= 202.5f && angle < 247.5f)
            return new Vector2(-0.7f, -0.7f); // Вниз-влево
        else if (angle >= 247.5f && angle < 292.5f)
            return Vector2.down;             // Вниз (0, -1)
        else if (angle >= 292.5f && angle < 337.5f)
            return new Vector2(0.7f, -0.7f);  // Вниз-вправо
        else
            return Vector2.down;
    }

    // Добавьте метод для логирования параметров аниматора:
    private void LogAnimatorParameters()
    {
        if (_animator != null && _showDebugLogs)
        {
            Debug.Log($"🎭 Animator Params - RollX: {_animator.GetFloat(_rollHorizontalParameter):F2}, " +
                      $"RollY: {_animator.GetFloat(_rollVerticalParameter):F2}, " +
                      $"IsRolling: {_animator.GetBool(_rollBlendTreeParameter)}, " +
                      $"Speed: {_animator.GetFloat("Speed"):F2}");
        }
    }

    private IEnumerator RollSequence()
    {
        float elapsedTime = 0f;
        Vector2 fixedDirection = _rollDirection;

        // Блокируем ввод и физику
        _moveInput = Vector2.zero;
        if (_rigidbody != null) _rigidbody.velocity = Vector2.zero;

        if (_showDebugLogs)
            Debug.Log($"🔄 Roll started: dir=({fixedDirection.x:F2}, {fixedDirection.y:F2}), duration={_rollDuration}s");

        while (elapsedTime < _rollDuration && !_isDead && !_movementFrozen)
        {
            // 🔥 Обновляем анимацию КАЖДЫЙ КАДР — даже если FPS низкий
            if (_animator != null)
            {
                _animator.SetFloat(_rollHorizontalParameter, fixedDirection.x);
                _animator.SetFloat(_rollVerticalParameter, fixedDirection.y);
                _animator.SetBool(_rollBlendTreeParameter, true);
                _animator.Update(0); // ← Это ключевой вызов!
            }

            // Движение через MovePosition (плавнее)
            if (_rigidbody != null)
            {
                _rigidbody.MovePosition(_rigidbody.position + fixedDirection * _rollSpeed * Time.deltaTime);
            }

            elapsedTime += Time.deltaTime;
            yield return null; // ← Не WaitForFixedUpdate!
        }

        EndRoll();
    }

    private void DebugRollDirection()
    {
        if (_animator != null && _showDebugLogs)
        {
            float rollX = _animator.GetFloat(_rollHorizontalParameter);
            float rollY = _animator.GetFloat(_rollVerticalParameter);
            bool isRolling = _animator.GetBool(_rollBlendTreeParameter);

            Debug.Log($"🎭 Roll Animation Params | IsRolling: {isRolling} | RollX: {rollX:F2} | RollY: {rollY:F2} | Target: ({_rollDirection.x:F2}, {_rollDirection.y:F2})");
        }
    }

    private IEnumerator ApplyInvincibility()
    {
        _isInvincible = true;

        // Визуальный эффект неуязвимости (мигание)
        if (_playerSpriteRenderer != null)
        {
            float flashInterval = 0.1f;
            float elapsed = 0f;
            Color originalColor = _playerSpriteRenderer.color;
            Color invincibleColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);

            while (elapsed < _invincibilityDuration)
            {
                _playerSpriteRenderer.color = (_playerSpriteRenderer.color == originalColor) ? invincibleColor : originalColor;
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            _playerSpriteRenderer.color = originalColor;
        }

        _isInvincible = false;
    }

    public void EndRoll()
    {

        _isRolling = false;
        _pendingRollRequest = false; // ← сбрасываем запрос при завершении

        if (_isRolling)
        {
            _isRolling = false;

            // Устанавливаем кулдаун
            _currentRollCooldown = _rollCooldown;
           // _canRoll = false; // ← пока кулдаун идёт — нельзя кататься

            // Сбрасываем анимацию
            if (_animator != null)
            {
                _animator.SetBool(_rollBlendTreeParameter, false);

                // Сбрасываем параметры направления
                _animator.SetFloat(_rollHorizontalParameter, 0);
                _animator.SetFloat(_rollVerticalParameter, 0);

                if (_showDebugLogs)
                    Debug.Log($"🎭 EndRoll: Reset animation parameters");
            }

            // Вызываем событие
            OnRollFinished?.Invoke();

            // Очищаем корутину
            if (_rollCoroutine != null)
            {
                StopCoroutine(_rollCoroutine);
                _rollCoroutine = null;
            }

            if (_showDebugLogs)
                Debug.Log($"✅ Roll finished, cooldown: {_rollCooldown}s, canRoll: {_canRoll}");
        }
    }

    public void ResetRoll()
    {
        _isRolling = false;
        _canRoll = true;
        _currentRollCooldown = 0f;

        if (_rollCoroutine != null)
        {
            StopCoroutine(_rollCoroutine);
            _rollCoroutine = null;
        }

        if (_animator != null)
        {
            _animator.SetBool(_rollBlendTreeParameter, false);
        }

        Debug.Log($"🔄 Roll state reset");
    }


    private void OnDrawGizmos()
    {
        // Визуализация округленного направления переката
        if (Application.isPlaying && _isRolling)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)_rollDirection * 1.5f);
            Gizmos.DrawWireSphere(transform.position + (Vector3)_rollDirection * 1.5f, 0.1f);

            // Также показываем неокругленное направление для сравнения
            Vector2 rawDirection;
            if (_moveInput.magnitude > 0.1f)
            {
                rawDirection = _moveInput.normalized;
            }
            else
            {
                rawDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)rawDirection * 1.2f);
        }
    }


    // Метод для проверки неуязвимости (может использоваться другими системами)
    public bool IsInvincible()
    {
        return _isInvincible;
    }




}