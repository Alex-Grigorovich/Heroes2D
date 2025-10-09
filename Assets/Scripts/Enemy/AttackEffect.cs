using UnityEngine;

public class AttackEffect : MonoBehaviour
{
    [SerializeField] private float _lifetime = 0.8f;

    void Start()
    {
        Destroy(gameObject, _lifetime);
    }
}