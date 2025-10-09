using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private float _knockbackForce = 5f;

    private int _currentHealth;
    private Rigidbody2D _rb;
    private Animator _animator;

    void Start()
    {
        _currentHealth = _maxHealth;
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    public void TakeDamage(int damage, Vector2 attackDirection)
    {
        _currentHealth -= damage;

        // Анимация получения урона
        if (_animator != null)
        {
            _animator.SetTrigger("TakeDamage");
        }

        // Отбрасывание
        if (_rb != null)
        {
            _rb.AddForce(attackDirection * _knockbackForce, ForceMode2D.Impulse);
        }

        // Проверка смерти
        if (_currentHealth <= 0)
        {
            Die();
        }

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {_currentHealth}/{_maxHealth}");
    }

    private void Die()
    {
        // Анимация смерти
        if (_animator != null)
        {
            _animator.SetBool("IsDead", true);
        }

        // Отключаем компоненты
        if (_rb != null) _rb.simulated = false;
        GetComponent<Collider2D>().enabled = false;

        // Уничтожаем через 2 секунды
        Destroy(gameObject, 2f);

        Debug.Log($"{gameObject.name} died!");
    }
}