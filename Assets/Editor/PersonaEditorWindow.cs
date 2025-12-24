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
        Vector2 _personaScroll;
        Dictionary<string, Texture2D> _thumbCache = new Dictionary<string, Texture2D>();
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
                float thumbSize = 48f;
                var thumb = GetThumbnail(p);
                if (thumb != null)
                {
                    GUILayout.Label(thumb, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                }
                else
                {
                    GUILayout.Box(string.Empty, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                }

                string label = string.IsNullOrEmpty(p.displayName) ? p.characterId : p.displayName;
                var buttonStyle = new GUIStyle(GUI.skin.button);
                if (i == _selectedIndex)
                {
                    buttonStyle.normal.textColor = Color.white;
                    buttonStyle.normal.background = Texture2D.grayTexture;
                }
                if (GUILayout.Button(label, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(thumbSize)))
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
            float labelWidth = 100f;
            float fieldWidth = Mathf.Min(position.width - 300f, 400f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Character Id", GUILayout.Width(labelWidth));
            p.characterId = EditorGUILayout.TextField(p.characterId, GUILayout.Width(fieldWidth));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Display Name", GUILayout.Width(labelWidth));
            p.displayName = EditorGUILayout.TextField(p.displayName, GUILayout.Width(fieldWidth));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Speaker Id", GUILayout.Width(labelWidth));
            p.speakerId = EditorGUILayout.TextField(p.speakerId, GUILayout.Width(fieldWidth));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Persona", EditorStyles.label);
            var wrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _personaScroll = EditorGUILayout.BeginScrollView(_personaScroll, GUILayout.Height(160), GUILayout.Width(fieldWidth + labelWidth));
            p.persona = EditorGUILayout.TextArea(p.persona, wrapStyle, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Image Path", GUILayout.Width(labelWidth));
            p.imagePath = EditorGUILayout.TextField(p.imagePath, GUILayout.Width(fieldWidth));
            EditorGUILayout.EndHorizontal();
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
            _thumbCache.Clear();

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
                    _ = LoadAllThumbnailsAsync();
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
                _ = LoadAllThumbnailsAsync();
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
                _previewTexture = GetThumbnail(p);
                if (_previewTexture == null)
                {
                    var handle = UnityAddressables.LoadAssetAsync<Texture2D>(p.imagePath);
                    _previewTexture = await handle.Task;
                    UnityAddressables.Release(handle);
                    if (_previewTexture != null)
                    {
                        _thumbCache[NormalizePath(p.imagePath)] = _previewTexture;
                    }
                }
            }
            catch
            {
                _previewTexture = null;
            }
            Repaint();
        }

        Texture2D GetThumbnail(PersonaEntry p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.imagePath))
            {
                return null;
            }
            string key = NormalizePath(p.imagePath);
            if (_thumbCache.TryGetValue(key, out var tex))
            {
                return tex;
            }
            var assetPath = key.StartsWith("Assets/") ? key : $"Assets/{key}";
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (loaded != null)
            {
                _thumbCache[key] = loaded;
            }
            return loaded;
        }

        string NormalizePath(string path)
        {
            return path.Replace("\\", "/");
        }

        async Task LoadAllThumbnailsAsync()
        {
            foreach (var p in _personas)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.imagePath))
                {
                    continue;
                }
                string key = NormalizePath(p.imagePath);
                if (_thumbCache.ContainsKey(key))
                {
                    continue;
                }
                try
                {
                    var handle = UnityAddressables.LoadAssetAsync<Texture2D>(p.imagePath);
                    var tex = await handle.Task;
                    UnityAddressables.Release(handle);
                    if (tex != null)
                    {
                        _thumbCache[key] = tex;
                    }
                }
                catch { }
            }
            Repaint();
        }
    }
}
