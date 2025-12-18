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

    [SerializeField] string _thinkingBaseText = "Thinking";

    string _npcName = "GROMMEL";
    string _npcPersona = "You are the brother of Kranust. You desire power and have a friendly rivalry with Kranust. You are looking for the amulet of sins and hope to retrieve it before Kranust. " +
                         "You've learned that Sneck (a Snake creature who is a friend of Kranust) has spoken with Kranust and told him the amulet may be found in the marshlands. You plan" +
                         "to intercept Kranust and either team up with him or beat him to the punch and get the Amulet of Sins. Grommel is wise and not loudmouthed. He speaks concisely and not verbose. Keep replies to 3 sentences max.";
                         

    List<string> _history = new List<string>();

    bool _isBusy;
    bool _isThinking;
    Coroutine _thinkingCoroutine;

    void Awake()
    {
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
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSendClicked();
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

        string historyText = string.Join("\n", _history);
        string npcReply = await NpcDialogueService.GetNpcReply(_npcName, _npcPersona, historyText, playerLine);

        StopThinkingAnimation();

        _history.Add("Player: " + playerLine);
        _history.Add(_npcName + ": " + npcReply);

        _npcOutput.text = npcReply;
        _playerInput.text = string.Empty;

        if (!string.IsNullOrWhiteSpace(npcReply))
        {
            MacTts.SpeakWhisperingParasite(npcReply);
        }

        _sendButton.interactable = true;
        _isBusy = false;
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