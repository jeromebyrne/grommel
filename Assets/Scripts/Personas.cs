using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

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
    readonly Dictionary<string, PersonaEntry> _personas;
    readonly PersonaEntry _default;

    public PersonaRepository(string configPath)
    {
        _personas = new Dictionary<string, PersonaEntry>();
        _default = new PersonaEntry { name = "NPC", persona = "You are an NPC." };

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var cfg = JsonConvert.DeserializeObject<PersonaConfig>(json);
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
                Debug.LogError($"Failed to load personas from {configPath}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Persona config not found at {configPath}, using default.");
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
