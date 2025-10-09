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

        // �������� ��������� �����
        if (_animator != null)
        {
            _animator.SetTrigger("TakeDamage");
        }

        // ������������
        if (_rb != null)
        {
            _rb.AddForce(attackDirection * _knockbackForce, ForceMode2D.Impulse);
        }

        // �������� ������
        if (_currentHealth <= 0)
        {
            Die();
        }

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {_currentHealth}/{_maxHealth}");
    }

    private void Die()
    {
        // �������� ������
        if (_animator != null)
        {
            _animator.SetBool("IsDead", true);
        }

        // ��������� ����������
        if (_rb != null) _rb.simulated = false;
        GetComponent<Collider2D>().enabled = false;

        // ���������� ����� 2 �������
        Destroy(gameObject, 2f);

        Debug.Log($"{gameObject.name} died!");
    }
}