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
    [SerializeField] private float _targetZoneRadius = 0.5f; // Радиус зоны поражения вокруг TargetZone

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
    private bool _isDealingDamage = false; // Флаг что сейчас наносится урон
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

        // Создаем ретиклу наведения
        if (_showTargetingReticle && _targetingReticlePrefab != null)
        {
            _targetingReticle = Instantiate(_targetingReticlePrefab);
            _targetingReticle.SetActive(false);
        }
    }

    void Update()
    {
        UpdateMousePosition();
        UpdateTargeting();
        UpdateCooldowns();
        HandleAttackInput();
        UpdateAnimation();
        UpdateTargetingReticle();

        // Проверяем попадание по врагам в реальном времени, когда наносится урон
        if (_isDealingDamage && _targetZoneInstance != null)
        {
            CheckTargetZoneDamage();
        }
    }

    private void CheckTargetZoneDamage()
    {
        if (_targetZoneInstance == null) return;

        // Ищем всех врагов в радиусе TargetZone
        Collider2D[] enemiesInZone = Physics2D.OverlapCircleAll(
            _targetZoneInstance.transform.position,
            _targetZoneRadius,
            _enemyLayer
        );

        int hits = 0;

        foreach (Collider2D enemy in enemiesInZone)
        {
            if (!enemy.CompareTag("Enemy")) continue;

            // Проверяем, не наносили ли уже урон этому врагу в этой атаке
            if (_alreadyDamagedEnemies.Contains(enemy.gameObject)) continue;

            // Проверяем шанс попадания
            if (CheckHitChance(enemy.gameObject))
            {
                DealSingleDamage(enemy.gameObject);
                _alreadyDamagedEnemies.Add(enemy.gameObject); // Помечаем как получившего урон
                hits++;

                // Ограничиваем количество целей за удар
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
        // Получаем позицию мыши в мировых координатах
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -_mainCamera.transform.position.z;
        _mouseWorldPosition = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }

    private void UpdateTargeting()
    {
        if (_autoTargetClosestEnemy)
        {
            FindBestTarget();
        }
        else
        {
            // Базовое направление к курсору
            _attackDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
        }
    }

    private void FindBestTarget()
    {
        // Ищем всех врагов в радиусе наведения
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, _targetingRadius, _enemyLayer);

        GameObject bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider2D enemy in nearbyEnemies)
        {
            if (!enemy.CompareTag("Enemy")) continue;

            Vector2 toEnemy = (Vector2)enemy.transform.position - _mouseWorldPosition;
            float distanceToCursor = toEnemy.magnitude;

            // Вычисляем "ценность" цели (чем ближе к курсору - тем лучше)
            float score = distanceToCursor;

            // Предпочитаем цели, которые ближе к игроку
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
            // Направление атаки - к выбранной цели
            _attackDirection = ((Vector2)_currentTarget.transform.position - (Vector2)transform.position).normalized;
        }
        else
        {
            // Если цели нет - направление к курсору
            _attackDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
        }
    }

    private void UpdateTargetingReticle()
    {
        if (_targetingReticle != null)
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
    }

    private void UpdateCooldowns()
    {
        if (_currentAttackCooldown > 0f)
        {
            _currentAttackCooldown -= Time.deltaTime;
        }
    }

    private void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            StartAttack();
        }
    }

    private bool CanAttack()
    {
        return _currentAttackCooldown <= 0f && !_isAttacking;
    }

    private void StartAttack()
    {
        _isAttacking = true;
        _currentAttackCooldown = _attackCooldown;

        // Очищаем список уже получивших урон врагов
        _alreadyDamagedEnemies.Clear();

        _animator.SetBool("IsAttacking", true);

        // Создаем и показываем зону атаки в направлении удара
        CreateTargetZone();

        StartCoroutine(AttackSequence());

        Debug.Log($"🎯 Attack started! Direction: {_attackDirection}");
    }

    private void CreateTargetZone()
    {
        if (_targetZonePrefab != null)
        {
            // Уничтожаем старую зону, если она есть
            DestroyTargetZone();

            // Вычисляем позицию зоны атаки ВОКРУГ персонажа
            Vector3 targetZonePosition = transform.position + (Vector3)_attackDirection * _targetZoneDistance;

            // Создаем новую зону атаки
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
        else
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
        // Ждем начала анимации удара
        yield return new WaitForSeconds(_attackDuration * 0.2f);

        // Начинаем наносить урон
        _isDealingDamage = true;
        Debug.Log("💥 START dealing damage phase");

        // Период нанесения урона (основная часть анимации)
        yield return new WaitForSeconds(_attackDuration * 0.4f);

        // Заканчиваем наносить урон
        _isDealingDamage = false;
        Debug.Log("💥 END dealing damage phase");

        // Уничтожаем зону атаки
        DestroyTargetZone();

        // Завершаем атаку
        yield return new WaitForSeconds(_attackDuration * 0.4f);
        EndAttack();
    }

    // Новый метод для одинарного урона через TargetZone
    private void DealSingleDamage(GameObject enemy)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Vector2 directionToEnemy = (enemy.transform.position - transform.position).normalized;

            // ОДИНАРНЫЙ урон (базовый урон без множителей)
            int finalDamage = CalculateDiabloDamage();
            bool isCritical = CheckCriticalStrike();

            // Критический урон применяется как обычно
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

    // Старый метод для обратной совместимости
    private void DealDiabloStyleDamage(GameObject enemy)
    {
        DealSingleDamage(enemy); // Теперь используем одинарный урон
    }

    private bool CheckHitChance(GameObject enemy)
    {
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
        int baseDamage = UnityEngine.Random.Range(_minDamage, _maxDamage + 1);
        int finalDamage = baseDamage;
        Debug.Log($"⚔️ Damage roll: {baseDamage} (range: {_minDamage}-{_maxDamage})");
        return finalDamage;
    }

    private bool CheckCriticalStrike()
    {
        bool isCritical = UnityEngine.Random.Range(0f, 1f) <= _criticalChance;
        if (isCritical) Debug.Log($"🎲 Critical strike! Chance: {_criticalChance * 100}%");
        return isCritical;
    }

    private void ShowDamageEffect(Vector3 position, int damage, bool isCritical)
    {
        StartCoroutine(FlashEnemy(position, isCritical));
        CreateDamagePopup(position, damage, isCritical);

        // УБРАЛИ мигание персонажа - оно не нужно для игрока при атаке
        // FlashPlayer();
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

    private void EndAttack()
    {
        _isAttacking = false;
        _isDealingDamage = false;
        _animator.SetBool("IsAttacking", false);

        // Очищаем список поврежденных врагов при завершении атаки
        _alreadyDamagedEnemies.Clear();

        // Убеждаемся, что цвет игрока сброшен (на всякий случай)
        ResetPlayerColor();
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
        Vector2 animationDirection;

        if (_isAttacking)
        {
            animationDirection = _attackDirection;
        }
        else
        {
            if (_moveInput.magnitude > 0.1f)
            {
                animationDirection = _moveInput.normalized;
            }
            else
            {
                animationDirection = (_mouseWorldPosition - (Vector2)transform.position).normalized;
            }
        }

        _animator.SetFloat("Horizontal", animationDirection.x);
        _animator.SetFloat("Vertical", animationDirection.y);
        _animator.SetFloat("Speed", _moveInput.magnitude);
    }

    private void FixedUpdate()
    {
        if (!_isAttacking)
        {
            Move();
        }
        else
        {
            _velocity = Vector2.zero;
            _rigidbody.velocity = Vector2.zero;
        }
    }

    private void Move()
    {
        Vector2 targetVelocity = _moveInput * _MaxSpeed;
        Vector2 velocityDiff = targetVelocity - _velocity;
        float accelerateRate = (Mathf.Abs(targetVelocity.magnitude) > 0.01f) ? _acceleration : _deceleration;

        Vector2 movement = velocityDiff * (accelerateRate * Time.fixedDeltaTime);

        _velocity += movement;
        _velocity = Vector2.ClampMagnitude(_velocity, _MaxSpeed);
        _velocity *= MathF.Pow(1f - _velocityPower, Time.fixedDeltaTime);

        _rigidbody.MovePosition(_rigidbody.position + _velocity * Time.fixedDeltaTime);
        _rigidbody.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed && CanAttack())
        {
            StartAttack();
        }
    }

    // Добавляем методы для управления жизненным циклом
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
        // Радиус атаки
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRadius);

        // Радиус наведения
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _targetingRadius);

        // Радиус TargetZone (зона поражения)
        if (Application.isPlaying && _targetZoneInstance != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_targetZoneInstance.transform.position, _targetZoneRadius);
        }

        // Радиус выбора цели вокруг курсора
        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_mouseWorldPosition, _targetSelectionRadius);
        }

        // Угол атаки
        if (Application.isPlaying && _isAttacking)
        {
            Gizmos.color = Color.red;
            DrawAttackAngleGizmo();
        }

        // Линия к текущей цели
        if (Application.isPlaying && _currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
        }

        // Позиция TargetZone
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