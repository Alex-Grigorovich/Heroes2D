using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

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

    // Свойство для доступа из других систем
    public bool IsAttacking => _isAttacking;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _mainCamera = Camera.main;
        _playerSpriteRenderer = GetComponent<SpriteRenderer>();

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
    }

    private void Update()
    {
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

        // Проверяем состояние урона
        bool isHurting = false;
        if (_healthSystem != null)
        {
            isHurting = _healthSystem.IsHurting();
        }

        // Если персонаж мертв, движение заморожено или получает урон - отключаем управление
        if (_isDead || _movementFrozen || isHurting)
        {
            DisableMovement();

            // Если получаем урон, прерываем атаку
            if (isHurting && _isAttacking)
            {
                EndAttack();
            }

            return;
        }

        UpdateMousePosition();
        UpdateTargeting();
        UpdateCooldowns();
        HandleAttackInput();
        UpdateAnimation();
        UpdateTargetingReticle();

        if (_isDealingDamage && _targetZoneInstance != null)
        {
            CheckTargetZoneDamage();
        }
    }




    private bool IsMovementBlocked()
    {
        return HealthSystem.Instance != null &&
               (HealthSystem.Instance.IsDead() ||
                HealthSystem.Instance.IsHurting() ||  // Уже есть
                IsMovementFrozenByHealthSystem());
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
        if (_isDead || _movementFrozen) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -_mainCamera.transform.position.z;
        _mouseWorldPosition = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }

    private void UpdateTargeting()
    {
        if (_isDead || _movementFrozen) return;

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
        if (_isDead || _movementFrozen) return;

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

        yield return new WaitForSeconds(_attackDuration * 0.2f);

        if (_isDead || _movementFrozen)
        {
            EndAttack();
            yield break;
        }

        _isDealingDamage = true;
        Debug.Log("💥 START dealing damage phase");

        yield return new WaitForSeconds(_attackDuration * 0.4f);

        if (_isDead || _movementFrozen)
        {
            _isDealingDamage = false;
            EndAttack();
            yield break;
        }

        _isDealingDamage = false;
        Debug.Log("💥 END dealing damage phase");

        DestroyTargetZone();

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
        if (_isDead || _movementFrozen)
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
            }
            return;
        }

        // Проверяем, получает ли персонаж урон
        bool isHurting = false;
        if (_healthSystem != null)
        {
            isHurting = _healthSystem.IsHurting();
        }

        // ПРИОРИТЕТ: Урон > Атака > Движение > Наведение
        if (isHurting)
        {
            // Во время получения урона - не обновляем анимацию
            // HealthSystem уже управляет аниматором через свои параметры
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f); // Останавливаем движение
                _animator.SetBool("IsAttacking", false); // Отменяем атаку
            }
            return;
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
            }
        }
    }

    private void FixedUpdate()
    {
        // Проверяем состояние урона
        bool isHurting = false;
        if (_healthSystem != null)
        {
            isHurting = _healthSystem.IsHurting();
        }

        if (_isDead || _movementFrozen || isHurting)
        {
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
            return;
        }

        if (!_isAttacking)
        {
            Move();
        }
        else
        {
            _velocity = Vector2.zero;
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector2.zero;
            }
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
        if (_isDead || _movementFrozen || !enabled)
        {
            _moveInput = Vector2.zero;
            return;
        }

        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (_isDead || _movementFrozen || !enabled) return;

        if (context.performed && CanAttack())
        {
            StartAttack();
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



}