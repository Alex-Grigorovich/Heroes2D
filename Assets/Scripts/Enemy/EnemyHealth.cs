using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private float _knockbackForce = 3f;

    private int _currentHealth;
    private Rigidbody2D _rb;
    private SpriteRenderer _sprite;

    void Start()
    {
        _currentHealth = _maxHealth;
        _rb = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();

        // Автоматически ставим тег если забыли
        if (!gameObject.CompareTag("Enemy"))
        {
            gameObject.tag = "Enemy";
            Debug.Log($"Auto-assigned Enemy tag to {gameObject.name}");
        }
    }

    public void TakeDamage(int damage, Vector2 attackDirection)
    {
        // Проверяем компоненты
        if (_sprite == null) _sprite = GetComponent<SpriteRenderer>();
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        _currentHealth -= damage;

        Debug.Log($"{gameObject.name} took {damage} damage! Health: {_currentHealth}/{_maxHealth}");

        // Визуальная обратная связь
        StartCoroutine(FlashRed());

        // Отбрасывание (если есть Rigidbody)
        if (_rb != null)
        {
            _rb.AddForce(attackDirection * _knockbackForce, ForceMode2D.Impulse);
        }

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private System.Collections.IEnumerator FlashRed()
    {
        if (_sprite != null)
        {
            Color originalColor = _sprite.color;
            _sprite.color = Color.red;
            yield return new WaitForSeconds(0.2f);
            _sprite.color = originalColor;
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died!");
        Destroy(gameObject);
    }
}