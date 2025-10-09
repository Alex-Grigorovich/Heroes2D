using UnityEngine;

public class EnemyController8Directions : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRange = 5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private LayerMask _obstacleLayer = 1;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _stoppingDistance = 1f;

    private Transform _player;
    private Rigidbody2D _rb;
    private Animator _animator;
    private bool _isPlayerDetected = false;
    private Vector2 _lastValidDirection = Vector2.down;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // Находим игрока по тегу
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }

        // Настройка Rigidbody для 2D
        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void Update()
    {
        if (_player == null) return;

        CheckForPlayer();

        if (_isPlayerDetected)
        {
            MoveTowardsPlayer();
        }
        else
        {
            StopMoving();
        }

        UpdateAnimation();
    }

    void CheckForPlayer()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        if (distanceToPlayer <= _detectionRange)
        {
            // Проверяем, нет ли препятствий между врагом и игроком
            Vector2 directionToPlayer = (_player.position - transform.position).normalized;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer, _detectionRange, _obstacleLayer);

            if (hit.collider == null || hit.collider.CompareTag("Player"))
            {
                _isPlayerDetected = true;
                _lastValidDirection = directionToPlayer; // Сохраняем корректное направление
            }
            else
            {
                _isPlayerDetected = false;
            }
        }
        else
        {
            _isPlayerDetected = false;
        }
    }

    void MoveTowardsPlayer()
    {
        Vector2 direction = (_player.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        // Сохраняем направление для анимации
        _lastValidDirection = direction;

        // Двигаемся только если игрок вне радиуса остановки
        if (distanceToPlayer > _stoppingDistance)
        {
            Vector2 movement = direction * _moveSpeed;

            if (_rb != null)
            {
                _rb.velocity = movement;
            }
            else
            {
                transform.position = Vector2.MoveTowards(transform.position, _player.position, _moveSpeed * Time.deltaTime);
            }
        }
        else
        {
            StopMoving();
        }
    }

    void StopMoving()
    {
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
        }
    }

    void UpdateAnimation()
    {
        if (_animator == null) return;

        Vector2 directionToPlayer = (_player.position - transform.position).normalized;
        float currentSpeed = _isPlayerDetected ? _moveSpeed : 0f;

        // Всегда используем актуальное направление к игроку
        _animator.SetFloat("Horizontal", directionToPlayer.x);
        _animator.SetFloat("Vertical", directionToPlayer.y);
        _animator.SetFloat("Speed", currentSpeed);
    }

    // Визуализация в редакторе
    private void OnDrawGizmosSelected()
    {
        // Зона обнаружения
        Gizmos.color = _isPlayerDetected ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        // Радиус остановки
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _stoppingDistance);

        // Направление движения
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, _lastValidDirection * 2f);
        }
    }
}