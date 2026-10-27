using UnityEngine;

// Общая основа для реакций мишени на удар (отброс, рассыпание и т.д.)
public abstract class TargetReaction : MonoBehaviour
{
    // attacker - позиция героя, от него мишень отбрасывает или разлетается
    public abstract void Play(Vector3 attacker);
}
