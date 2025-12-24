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

        void Awake()
        {
            _loader = new AddressablesLoader();
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
            }
            else
            {
                Debug.LogWarning($"Failed to load persona image at '{persona.imagePath}'");
            }
        }
    }
}
