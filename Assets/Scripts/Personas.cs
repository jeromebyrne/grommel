using System.Collections.Generic;
using System.Threading.Tasks;
using Grommel.Addressables;
using Newtonsoft.Json;
using UnityEngine;

namespace Grommel.Personas
{
    public class PersonaEntry
    {
        public string name;
        public string persona;
    }

    public class PersonaConfig
    {
        public Dictionary<string, PersonaEntry> entries;
    }

    public class PersonaRepository
    {
        Dictionary<string, PersonaEntry> _personas = new Dictionary<string, PersonaEntry>();
        PersonaEntry _default = new PersonaEntry { name = "NPC", persona = "You are an NPC." };
        readonly string _addressableKey;
        readonly IAddressablesLoader _loader;

        public PersonaRepository(string addressableKey, IAddressablesLoader loader)
        {
            _addressableKey = addressableKey;
            _loader = loader;
        }

        public async Task LoadAsync()
        {
            try
            {
                TextAsset asset = await _loader.LoadAssetAsync<TextAsset>(_addressableKey);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                {
                    Debug.LogWarning($"Persona asset '{_addressableKey}' not found or empty, using default.");
                    return;
                }

                var cfg = JsonConvert.DeserializeObject<PersonaConfig>(asset.text);
                if (cfg?.entries != null)
                {
                    _personas = cfg.entries;
                    if (_personas.TryGetValue("default", out var def))
                    {
                        _default = def;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load personas from '{_addressableKey}': {ex.Message}");
            }
        }

        public PersonaEntry Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return _default;
            }
            return _personas.TryGetValue(key, out var p) ? p : _default;
        }
    }
}
