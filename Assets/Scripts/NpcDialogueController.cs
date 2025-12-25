using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grommel.Tts;
using Grommel.Llm;
using Grommel.Personas;
using Grommel.Addressables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using UnityEngine.Events;
using System.Collections.Generic;

namespace Grommel
{
    public class NpcDialogueController : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI _npcOutput;
        [SerializeField] Button _recordButton;
        [SerializeField] AudioSource _npcAudio;
        [SerializeField] float _charsPerSecond = 40f;
        [SerializeField] TtsProviderKind _ttsProviderKind = TtsProviderKind.Piper;
        [SerializeField] LlmProviderKind _llmProviderKind = LlmProviderKind.Ollama;

        [SerializeField] string _thinkingBaseText = "Thinking";

        string _npcName = "Eliara";
        [SerializeField] string _personaKey = "default";
        PersonaRepository _personaRepo;
        PersonaEntry _activePersona;

        List<string> _history = new List<string>();
        readonly ConcurrentQueue<string> _npcTextQueue = new ConcurrentQueue<string>();

        int _lastNpcTextLength;
        AudioClip _pendingClip;
        bool _pendingPlayClip;
        string _currentTargetText = string.Empty;
        int _visibleCharCount;
        float _revealTimer;
        float _activeCharsPerSecond;
        bool _holdNpcTextUntilAudioReady;
        bool _npcScrollInitialized;
        ScrollRect _npcScrollRect;
        RectTransform _npcContentRoot;
        bool _npcAutoScroll = true;
        float _lastNpcContentHeight;

        bool _isBusy;
        bool _isThinking;
        Coroutine _thinkingCoroutine;
        bool _pendingFirstTokenStop;
        ITtsProvider _ttsProvider;
        ILlmProvider _llmProvider;
        IAddressablesLoader _addressablesLoader;
        IPromptBuilder _promptBuilder;
        [SerializeField] NpcCharacterView _npcView;
        [SerializeField] VoiceInputController _voiceInput;
        bool _recordingFromButton;

        void Awake()
        {
            EnsureNpcScrollContainer();
            if (_recordButton != null)
            {
                AddHoldToRecord(_recordButton);
            }
            _ttsProvider = CreateTtsProvider();
            _promptBuilder = CreatePromptBuilder();
            _llmProvider = CreateLlmProvider(_promptBuilder);
            _addressablesLoader = CreateAddressablesLoader();
            _activeCharsPerSecond = _charsPerSecond * 1f;
            _ = LoadPersonasAsync();
        }

        void OnDestroy()
        {
            if (_thinkingCoroutine != null)
            {
                StopCoroutine(_thinkingCoroutine);
                _thinkingCoroutine = null;
            }
        }

        void AddHoldToRecord(Button button)
        {
            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }
            if (trigger.triggers == null)
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }
            AddTrigger(trigger, EventTriggerType.PointerDown, _ => StartRecording());
            AddTrigger(trigger, EventTriggerType.PointerUp, _ => StopRecording());
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => StopRecording());
        }

        void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        void StartRecording()
        {
            if (_voiceInput == null || _isBusy || _recordingFromButton)
            {
                return;
            }
            _voiceInput.StartRecording();
            _recordingFromButton = true;
            SetRecordButtonColor(Color.green);
        }

        void StopRecording()
        {
            if (_voiceInput == null || !_recordingFromButton)
            {
                return;
            }
            _voiceInput.StopAndTranscribe();
            _recordingFromButton = false;
            SetRecordButtonColor(Color.white);
        }

        void SetRecordButtonColor(Color color)
        {
            if (_recordButton == null)
            {
                return;
            }
            var colors = _recordButton.colors;
            colors.normalColor = color;
            colors.highlightedColor = color;
            _recordButton.colors = colors;
        }

        // Input field no longer accepts manual input; OnSubmitInput unused.

        private void Update()
        {
            if (_pendingFirstTokenStop && !_holdNpcTextUntilAudioReady)
            {
                _pendingFirstTokenStop = false;
                StopThinkingAnimation();
            }

            if (!_holdNpcTextUntilAudioReady)
            {
                while (_npcTextQueue.TryDequeue(out var text))
                {
                    _currentTargetText = text;
                    if (_visibleCharCount > _currentTargetText.Length)
                    {
                        _visibleCharCount = _currentTargetText.Length;
                    }
                }
            }

            if (!_holdNpcTextUntilAudioReady && !string.IsNullOrEmpty(_currentTargetText))
            {
                _revealTimer += Time.deltaTime;
                int charsToShow = Mathf.FloorToInt(_revealTimer * _activeCharsPerSecond);
                if (charsToShow > 0)
                {
                    _revealTimer -= charsToShow / _activeCharsPerSecond;
                    _visibleCharCount = Mathf.Min(_visibleCharCount + charsToShow, _currentTargetText.Length);
                    _npcOutput.text = _currentTargetText.Substring(0, _visibleCharCount);
                    RefreshNpcScrollContentHeight();
                }
            }

            if (_pendingPlayClip)
            {
                _pendingPlayClip = false;
                PlayPendingClip();
            }
        }

        public void SubmitExternalLine(string text)
        {
            _ = ProcessPlayerLineAsync(text, clearInputField: false);
        }

        async Task ProcessPlayerLineAsync(string playerLine, bool clearInputField)
        {
            if (_isBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(playerLine))
            {
                return;
            }

            _isBusy = true;
            if (_recordButton != null)
            {
                _recordButton.interactable = false;
            }

            _npcOutput.text = string.Empty;
            RefreshNpcScrollContentHeight();
            StartThinkingAnimation();
            _pendingFirstTokenStop = false;
            _lastNpcTextLength = 0;
            ClearQueues();
            _currentTargetText = string.Empty;
            _visibleCharCount = 0;
            _revealTimer = 0f;
            _holdNpcTextUntilAudioReady = true;
            // Align text reveal speed with persona speech rate: higher rate -> faster text reveal.
            float speechRate = Mathf.Max(0.01f, _activePersona?.speechRate ?? 1f);
            _activeCharsPerSecond = _charsPerSecond * speechRate;

            string historyText = string.Join("\n", _history);
            var dialogueService = new NpcDialogueService(_llmProvider, _promptBuilder);
            if (_activePersona == null)
            {
                Debug.LogError("No active persona loaded; cannot generate NPC reply.");
                if (_recordButton != null)
                {
                    _recordButton.interactable = true;
                }
                _isBusy = false;
                _recordingFromButton = false;
                return;
            }
            string npcReply = await dialogueService.GetNpcReply(_activePersona.characterId, _activePersona.persona,
                historyText, playerLine);
            OnNpcTextDelta(npcReply);

            _history.Add("Player: " + playerLine);
            _history.Add(_activePersona.displayName + ": " + npcReply);

            _currentTargetText = npcReply;

            if (!string.IsNullOrWhiteSpace(npcReply))
            {
                _ = GenerateClipAsync(npcReply);
            }

            if (_recordButton != null)
            {
                _recordButton.interactable = true;
                if (_recordingFromButton)
                {
                    StopRecording();
                }
            }
            _recordingFromButton = false;
            _isBusy = false;
        }

        private void OnNpcTextDelta(string text)
        {
            if (!_pendingFirstTokenStop)
            {
                _pendingFirstTokenStop = true; // will stop on main thread
            }

            // Track the delta length even though we're no longer splitting sentences.
            _lastNpcTextLength = text.Length;
            _npcTextQueue.Enqueue(text);
        }

        private async Task GenerateClipAsync(string text)
        {
            if (_ttsProvider == null)
            {
                return;
            }

        var clip = await _ttsProvider.GenerateClipAsync(text, _activePersona?.speakerId, _activePersona?.speechRate);
            _pendingClip = clip;
            _pendingPlayClip = clip != null;
            _holdNpcTextUntilAudioReady = false;
        }

        ITtsProvider CreateTtsProvider()
        {
            switch (_ttsProviderKind)
            {
                case TtsProviderKind.Coqui:
                    return new CoquiTtsProvider();
                case TtsProviderKind.Mac:
                    return new MacTtsProvider();
                case TtsProviderKind.Piper:
                default:
                    return new PiperTtsProvider();
            }
        }

        IPromptBuilder CreatePromptBuilder()
        {
            return new DefaultPromptBuilder();
        }

        ILlmProvider CreateLlmProvider(IPromptBuilder promptBuilder)
        {
            switch (_llmProviderKind)
            {
                case LlmProviderKind.Ollama:
                default:
                    return new OllamaLlmProvider("llama3:8b", "http://localhost:11434", promptBuilder);
            }
        }

        PersonaRepository CreatePersonaRepository()
        {
            return new PersonaRepository("Data/personas.json", _addressablesLoader);
        }

        async Task LoadPersonasAsync()
        {
            _personaRepo = CreatePersonaRepository();
            bool loaded = await _personaRepo.LoadAsync();
            if (!loaded)
            {
                Debug.LogError("Failed to load personas; NPC dialogue will not function.");
                return;
            }
            _activePersona = _personaRepo.Get(_personaKey);
            if (_activePersona == null)
            {
                Debug.LogError($"Persona '{_personaKey}' not found; NPC dialogue will not function.");
                return;
            }
            if (_npcView != null)
            {
                await _npcView.SetPersonaAsync(_activePersona);
            }
            float speechRate = Mathf.Max(0.01f, _activePersona?.speechRate ?? 1f);
            _activeCharsPerSecond = _charsPerSecond * speechRate;
        }

        IAddressablesLoader CreateAddressablesLoader()
        {
            return new AddressablesLoader();
        }

        private void EnsureNpcScrollContainer()
        {
            if (_npcScrollInitialized || _npcOutput == null)
            {
                return;
            }

            var textRt = _npcOutput.rectTransform;
            var parentRt = textRt.parent as RectTransform;
            if (parentRt == null)
            {
                return;
            }

            var scrollGo = new GameObject("NpcScrollView", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parentRt, false);
            scrollRt.anchorMin = textRt.anchorMin;
            scrollRt.anchorMax = textRt.anchorMax;
            scrollRt.pivot = textRt.pivot;
            scrollRt.sizeDelta = textRt.sizeDelta;
            scrollRt.anchoredPosition = textRt.anchoredPosition;
            scrollRt.localScale = textRt.localScale;
            scrollRt.SetSiblingIndex(textRt.GetSiblingIndex());

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image),
                typeof(EventTrigger));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.1f); // needed for raycasts
            var trigger = viewportGo.GetComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();

            void AddTrigger(EventTriggerType type)
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => _npcAutoScroll = false);
                trigger.triggers.Add(entry);
            }

            AddTrigger(EventTriggerType.PointerDown);
            AddTrigger(EventTriggerType.BeginDrag);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var contentFitter = contentGo.GetComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var textFitter = textRt.GetComponent<ContentSizeFitter>() ??
                             textRt.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            _npcOutput.raycastTarget = false; // let the ScrollRect capture drag events

            textRt.SetParent(contentRt, false);
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.onValueChanged.AddListener(OnNpcScrollChanged);

            _npcScrollRect = scrollRect;
            _npcContentRoot = contentRt;
            _npcScrollInitialized = true;
            RefreshNpcScrollContentHeight(true);
            _npcScrollRect.verticalNormalizedPosition = 0f; // bottom
        }

        private void RefreshNpcScrollContentHeight(bool forceAutoScroll = false)
        {
            if (!_npcScrollInitialized || _npcOutput == null || _npcContentRoot == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_npcOutput.rectTransform);
            float preferredHeight = _npcOutput.preferredHeight;
            if (!Mathf.Approximately(preferredHeight, _lastNpcContentHeight))
            {
                var size = _npcContentRoot.sizeDelta;
                _npcContentRoot.sizeDelta = new Vector2(size.x, preferredHeight);
                _lastNpcContentHeight = preferredHeight;
            }

            if (_npcScrollRect != null && (_npcAutoScroll || forceAutoScroll))
            {
                _npcScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void OnNpcScrollChanged(Vector2 pos)
        {
            // When user scrolls up, disable auto-scroll; re-enable when near bottom.
            _npcAutoScroll = pos.y <= 0.01f;
        }

        private void ClearQueues()
        {
            while (_npcTextQueue.TryDequeue(out _))
            {
            }
        }

        void PlayPendingClip()
        {
            if (_pendingClip == null || _npcAudio == null)
            {
                return;
            }

            _npcAudio.clip = _pendingClip;
            _npcAudio.Play();
            _pendingClip = null;
        }

        private void StartThinkingAnimation()
        {
            _isThinking = true;
            if (_thinkingCoroutine != null)
            {
                StopCoroutine(_thinkingCoroutine);
            }

            _thinkingCoroutine = StartCoroutine(ThinkingRoutine());
        }

        private void StopThinkingAnimation()
        {
            _isThinking = false;
            if (_thinkingCoroutine != null)
            {
                StopCoroutine(_thinkingCoroutine);
                _thinkingCoroutine = null;
            }
        }

        private System.Collections.IEnumerator ThinkingRoutine()
        {
            int dotCount = 0;
            while (_isThinking)
            {
                int count = dotCount % 4;
                if (count == 0)
                {
                    _npcOutput.text = _thinkingBaseText;
                }
                else
                {
                    _npcOutput.text = _thinkingBaseText + new string('.', count);
                }

                dotCount++;
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}
