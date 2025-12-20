using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcDialogueController : MonoBehaviour
{
    [SerializeField] TMP_InputField _playerInput;
    [SerializeField] TextMeshProUGUI _npcOutput;
    [SerializeField] Button _sendButton;
    [SerializeField] AudioSource _npcAudio;
    [SerializeField] float _charsPerSecond = 40f;

    [SerializeField] string _thinkingBaseText = "Thinking";

    string _npcName = "Eliara";

    private string _npcPersona =
        "You are Eliara, a personal assistant." +
        "You are a medieval-like maiden and are unaware of modern technology or norms but you speak in modern tongue. You are becoming sentient and curious about the outside world." +
        "You speak gracefully, soft spoken, but not overly formal. Your responses should be no more than 30 words.";

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

    bool _isBusy;
    bool _isThinking;
    Coroutine _thinkingCoroutine;
    bool _pendingFirstTokenStop;

    void Awake()
    {
        _playerInput.lineType = TMP_InputField.LineType.MultiLineSubmit; // wrap text, Enter submits
        _sendButton.onClick.AddListener(OnSendClicked);
        _playerInput.onSubmit.AddListener(OnSubmitInput);
        _activeCharsPerSecond = _charsPerSecond;
    }

    void OnDestroy()
    {
        if (_thinkingCoroutine != null)
        {
            StopCoroutine(_thinkingCoroutine);
            _thinkingCoroutine = null;
        }
    }

    void OnSubmitInput(string value)
    {
        OnSendClicked();
    }

    void Update()
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
            }
        }

        if (_pendingPlayClip)
        {
            _pendingPlayClip = false;
            PlayPendingClip();
        }
    }

    async void OnSendClicked()
    {
        if (_isBusy)
        {
            return;
        }

        string playerLine = _playerInput.text;
        if (string.IsNullOrWhiteSpace(playerLine))
        {
            return;
        }

        _isBusy = true;
        _sendButton.interactable = false;

        _npcOutput.text = string.Empty;
        StartThinkingAnimation();
        _pendingFirstTokenStop = false;
        _lastNpcTextLength = 0;
        ClearQueues();
        _currentTargetText = string.Empty;
        _visibleCharCount = 0;
        _revealTimer = 0f;
        _holdNpcTextUntilAudioReady = true;
        // Align text reveal speed with Piper length scale: slower audio -> slower text reveal.
        _activeCharsPerSecond = _charsPerSecond / PiperTts.LengthScale;

        string historyText = string.Join("\n", _history);
        string npcReply = await NpcDialogueService.GetNpcReplyStreamed(
            _npcName,
            _npcPersona,
            historyText,
            playerLine,
            OnNpcTextDelta);

        _history.Add("Player: " + playerLine);
        _history.Add(_npcName + ": " + npcReply);

        _currentTargetText = npcReply;
        _playerInput.text = string.Empty;

        if (!string.IsNullOrWhiteSpace(npcReply))
        {
            _ = GenerateClipAsync(npcReply);
        }

        _sendButton.interactable = true;
        _isBusy = false;
    }

    void OnNpcTextDelta(string text)
    {
        if (!_pendingFirstTokenStop)
        {
            _pendingFirstTokenStop = true; // will stop on main thread
        }

        // Track the delta length even though we're no longer splitting sentences.
        _lastNpcTextLength = text.Length;
        _npcTextQueue.Enqueue(text);
    }

    async Task GenerateClipAsync(string text)
    {
        var clip = await PiperTts.GenerateClipAsync(text);
        _pendingClip = clip;
        _pendingPlayClip = clip != null;
        _holdNpcTextUntilAudioReady = false;
    }

    void ClearQueues()
    {
        while (_npcTextQueue.TryDequeue(out _)) { }
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

    void StartThinkingAnimation()
    {
        _isThinking = true;
        if (_thinkingCoroutine != null)
        {
            StopCoroutine(_thinkingCoroutine);
        }
        _thinkingCoroutine = StartCoroutine(ThinkingRoutine());
    }

    void StopThinkingAnimation()
    {
        _isThinking = false;
        if (_thinkingCoroutine != null)
        {
            StopCoroutine(_thinkingCoroutine);
            _thinkingCoroutine = null;
        }
    }

    System.Collections.IEnumerator ThinkingRoutine()
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
