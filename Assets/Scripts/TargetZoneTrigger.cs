using UnityEngine;
public class TargetZoneTrigger : MonoBehaviour
{
    public System.Action<Collider2D> OnEnemyEnter;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            OnEnemyEnter?.Invoke(other);
        }
    }
}