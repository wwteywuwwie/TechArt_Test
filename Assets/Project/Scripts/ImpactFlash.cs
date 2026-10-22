using UnityEngine;

/// <summary>
/// Короткая вспышка света в точке контакта. Вешается на корень префаба VFX_Impact.
/// </summary>
public class ImpactFlash : MonoBehaviour
{
    [SerializeField] Light flashLight;
    [SerializeField] float duration = 0.08f;

    void OnEnable()
    {
        if (flashLight) flashLight.enabled = true;
        Invoke(nameof(TurnOff), duration);
    }

    void TurnOff()
    {
        if (flashLight) flashLight.enabled = false;
    }
}
