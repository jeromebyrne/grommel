using System.Collections.Generic;
using System.Threading.Tasks;
using Grommel.Addressables;
using Newtonsoft.Json;
using UnityEngine;

namespace Grommel.Personas
{
    public class PersonaEntry
    {
        public string characterId;
        public string displayName;
        public string persona;
        public string imagePath;
    }

    public class PersonaConfig
    {
        public Dictionary<string, PersonaEntry> entries;
    }

    public class PersonaRepository
    {
        Dictionary<string, PersonaEntry> _personas = new Dictionary<string, PersonaEntry>();
        readonly string _addressableKey;
        readonly IAddressablesLoader _loader;

        public PersonaRepository(string addressableKey, IAddressablesLoader loader)
        {
            _addressableKey = addressableKey;
            _loader = loader;
        }

        public async Task<bool> LoadAsync()
        {
            try
            {
                TextAsset asset = await _loader.LoadAssetAsync<TextAsset>(_addressableKey);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                {
                    Debug.LogError($"Persona asset '{_addressableKey}' not found or empty.");
                    return false;
                }

                var root = JsonConvert.DeserializeObject<Dictionary<string, PersonaEntry>>(asset.text);
                if (root != null && root.Count > 0)
                {
                    _personas = new Dictionary<string, PersonaEntry>();
                    foreach (var kvp in root)
                    {
                        var entry = kvp.Value;
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.characterId))
                        {
                            _personas[entry.characterId.ToLowerInvariant()] = entry;
                        }
                    }
                    Debug.Log($"Loaded {_personas.Count} personas from '{_addressableKey}'.");
                    return true;
                }

                Debug.LogError($"Persona config '{_addressableKey}' missing entries.");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load personas from '{_addressableKey}': {ex.Message}");
                return false;
            }
        }

        public PersonaEntry Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("Persona key is null or empty.");
                return null;
            }
            var normalized = key.ToLowerInvariant();
            if (_personas.TryGetValue(normalized, out var p))
            {
                return p;
            }

            Debug.LogError($"Persona '{key}' not found in repository.");
            return null;
        }
    }
}
