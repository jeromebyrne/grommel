using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Concurrent;

public class NpcDialogueController : MonoBehaviour
{
    [SerializeField] TMP_InputField _playerInput;
    [SerializeField] TextMeshProUGUI _npcOutput;
    [SerializeField] Button _sendButton;
    [SerializeField] AudioSource _npcAudio;

    [SerializeField] string _thinkingBaseText = "Thinking";

    string _npcName = "Eliara";

    private string _npcPersona =
        "You are Eliara, a personal assistant." +
        "You are a medieval-like maiden and are unaware of modern technology or norms but you speak in modern tongue. You are becoming sentient and curious about the outside world." +
        "You speak gracefully, soft spoken, but not overly formal. Your responses should be no more than 30 words.";
                         

    List<string> _history = new List<string>();
    readonly ConcurrentQueue<string> _npcTextQueue = new ConcurrentQueue<string>();

    bool _isBusy;
    bool _isThinking;
    Coroutine _thinkingCoroutine;
    bool _pendingFirstTokenStop;

    void Awake()
    {
        _playerInput.lineType = TMP_InputField.LineType.MultiLineSubmit; // wrap text, Enter submits
        _sendButton.onClick.AddListener(OnSendClicked);
        _playerInput.onSubmit.AddListener(OnSubmitInput);
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
        if (_pendingFirstTokenStop)
        {
            _pendingFirstTokenStop = false;
            StopThinkingAnimation();
        }

        while (_npcTextQueue.TryDequeue(out var text))
        {
            _npcOutput.text = text;
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

        string historyText = string.Join("\n", _history);
        string npcReply = await NpcDialogueService.GetNpcReplyStreamed(
            _npcName,
            _npcPersona,
            historyText,
            playerLine,
            OnNpcTextDelta);

        _history.Add("Player: " + playerLine);
        _history.Add(_npcName + ": " + npcReply);

        _npcOutput.text = npcReply;
        _playerInput.text = string.Empty;

        // TTS temporarily disabled while focusing on streaming UI responsiveness.

        _sendButton.interactable = true;
        _isBusy = false;
    }

    void OnNpcTextDelta(string text)
    {
        if (!_pendingFirstTokenStop)
        {
            _pendingFirstTokenStop = true; // will stop on main thread
        }
        _npcTextQueue.Enqueue(text);
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
