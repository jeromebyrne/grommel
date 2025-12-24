using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Grommel.Personas;
using UnityEngine.AddressableAssets;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace Grommel.EditorTools
{
    public class PersonaEditorWindow : EditorWindow
    {
        const string PersonasAddressablePath = "Assets/Addressables/Data/personas.json";

        Vector2 _listScroll;
        Vector2 _detailScroll;
        List<PersonaEntry> _personas = new List<PersonaEntry>();
        int _selectedIndex = -1;
        string _status = string.Empty;
        Texture2D _previewTexture;

        [MenuItem("Grommel/Persona Editor")]
        public static void Open()
        {
            var window = GetWindow<PersonaEditorWindow>("Persona Editor");
            window.minSize = new Vector2(700, 400);
            window.Refresh();
        }

        void OnEnable()
        {
            Refresh();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Persona", GUILayout.Width(120)))
            {
                AddPersona();
            }
            if (GUILayout.Button("Save", GUILayout.Width(120)))
            {
                Save();
                _ = LoadPreviewAsync();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(240));
            EditorGUILayout.LabelField("Personas", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _personas.Count; i++)
            {
                var p = _personas[i];
                EditorGUILayout.BeginHorizontal("box");
                // preview thumbnail
                Texture2D thumb = (i == _selectedIndex) ? _previewTexture : null;
                float thumbSize = 48f;
                if (thumb != null)
                {
                    GUILayout.Label(thumb, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                }
                else
                {
                    GUILayout.Box(string.Empty, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                }

                string label = string.IsNullOrEmpty(p.displayName) ? p.characterId : p.displayName;
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true), GUILayout.Height(thumbSize)))
                {
                    _selectedIndex = i;
                    _ = LoadPreviewAsync();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawDetails()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
            if (_selectedIndex < 0 || _selectedIndex >= _personas.Count)
            {
                EditorGUILayout.HelpBox("Select a persona to edit, or add a new one.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var p = _personas[_selectedIndex];
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            p.characterId = EditorGUILayout.TextField("Character Id", p.characterId);
            p.displayName = EditorGUILayout.TextField("Display Name", p.displayName);
            p.speakerId = EditorGUILayout.TextField("Speaker Id", p.speakerId);
            EditorGUILayout.LabelField("Persona", EditorStyles.label);
            p.persona = EditorGUILayout.TextArea(p.persona, GUILayout.Height(150));
            p.imagePath = EditorGUILayout.TextField("Image Path", p.imagePath);
            if (_previewTexture != null)
            {
                float size = 120f;
                GUILayout.Label(_previewTexture, GUILayout.Width(size), GUILayout.Height(size));
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void AddPersona()
        {
            var entry = new PersonaEntry
            {
                characterId = "new_character",
                displayName = "New Character",
                persona = "Describe this character...",
                imagePath = string.Empty
            };
            _personas.Add(entry);
            _selectedIndex = _personas.Count - 1;
            _status = "Added new persona. Remember to Save.";
        }

        void Refresh()
        {
            _personas.Clear();
            _selectedIndex = -1;
            _status = string.Empty;
            _previewTexture = null;

            if (!File.Exists(PersonasAddressablePath))
            {
                _status = $"Personas file not found at {PersonasAddressablePath}. It will be created on Save.";
                return;
            }

            try
            {
                var json = File.ReadAllText(PersonasAddressablePath);
                var root = JsonConvert.DeserializeObject<Dictionary<string, PersonaEntry>>(json);
                if (root != null)
                {
                    foreach (var kvp in root)
                    {
                        if (kvp.Value != null)
                        {
                            // ensure characterId matches key if missing
                            if (string.IsNullOrWhiteSpace(kvp.Value.characterId))
                            {
                                kvp.Value.characterId = kvp.Key;
                            }
                            _personas.Add(kvp.Value);
                        }
                    }
                    if (_personas.Count > 0)
                    {
                        _selectedIndex = 0;
                    }
                    _status = $"Loaded {_personas.Count} personas.";
                    _ = LoadPreviewAsync();
                }
            }
            catch (System.Exception ex)
            {
                _status = $"Failed to load personas: {ex.Message}";
            }
        }

        void Save()
        {
            var dict = new Dictionary<string, PersonaEntry>();
            foreach (var p in _personas)
            {
                if (string.IsNullOrWhiteSpace(p.characterId))
                {
                    continue;
                }
                dict[p.characterId.ToLowerInvariant()] = p;
            }

            try
            {
                var json = JsonConvert.SerializeObject(dict, Formatting.Indented);
                var dir = Path.GetDirectoryName(PersonasAddressablePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(PersonasAddressablePath, json);
                AssetDatabase.ImportAsset(PersonasAddressablePath);
                _status = "Saved personas.";
                _ = LoadPreviewAsync();
            }
            catch (System.Exception ex)
            {
                _status = $"Failed to save personas: {ex.Message}";
            }
        }

        async Task LoadPreviewAsync()
        {
            _previewTexture = null;
            if (_selectedIndex < 0 || _selectedIndex >= _personas.Count)
            {
                Repaint();
                return;
            }

            var p = _personas[_selectedIndex];
            if (p == null || string.IsNullOrWhiteSpace(p.imagePath))
            {
                Repaint();
                return;
            }

            try
            {
                var handle = UnityAddressables.LoadAssetAsync<Texture2D>(p.imagePath);
                _previewTexture = await handle.Task;
                UnityAddressables.Release(handle);
            }
            catch
            {
                _previewTexture = null;
            }
            Repaint();
        }
    }
}
