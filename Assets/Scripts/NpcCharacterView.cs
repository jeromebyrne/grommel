using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Grommel.Personas;
using Grommel.Addressables;

namespace Grommel
{
public class NpcCharacterView : MonoBehaviour
{
    [SerializeField] Image _portrait;
    IAddressablesLoader _loader;
    [SerializeField] float _fadeDuration = 0.3f;

    void Awake()
    {
        _loader = new AddressablesLoader();
        if (_portrait != null)
        {
            var c = _portrait.color;
            c.a = 0f;
            _portrait.color = c;
        }
    }

    public async Task SetPersonaAsync(PersonaEntry persona)
    {
        if (persona == null || string.IsNullOrWhiteSpace(persona.imagePath) || _portrait == null)
        {
            Debug.LogError("Cannot set persona view: missing persona or image path.");
            return;
        }

        var sprite = await _loader.LoadAssetAsync<Sprite>(persona.imagePath);
        if (sprite != null)
        {
            _portrait.sprite = sprite;
            _portrait.preserveAspect = true;
            StartCoroutine(FadeIn());
        }
        else
        {
            Debug.LogWarning($"Failed to load persona image at '{persona.imagePath}'");
        }
    }

    System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        Color c = _portrait.color;
        float startAlpha = c.a;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            c.a = Mathf.Lerp(startAlpha, 1f, t);
            _portrait.color = c;
            yield return null;
        }
        c.a = 1f;
        _portrait.color = c;
    }
}
}
