using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Drives the on-screen code machine popup UI: number pad input, clear/submit, feedback
/// flash, and success/failure callbacks. Configured per-use via <see cref="Init"/> (called
/// by <see cref="UIPopupManager.ShowCodeMachine"/>).
/// </summary>
[MovedFrom(true, null, null, "CodeMachineListener")]
public class CodeMachineListener : MonoBehaviour
{
#if !UNITY_ANDROID && !UNITY_IOS
    // Desktop/console only: lets the player back out of the popup via a bound cancel action
    // (mobile falls back to polling Escape in Update, see below).
    [SerializeField] private InputActionReference cancelAction;
#endif

    [Header("Code Settings")]
    [SerializeField] private string correctCode = "1234";
    [SerializeField] private bool haveLimitedAttempts = true;

    [SerializeField] private int maxAttempts = 3;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI codeDisplay;
    [SerializeField] private Button[] numberButtons = new Button[10];
    [SerializeField] private Button clearButton;
    [SerializeField] private Button submitButton;

    [Header("Feedback")]
    [SerializeField] private Image feedbackImage;
    public AudioClip successSound;
    public AudioClip errorSound;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;
    /// <summary>How long the feedback image stays tinted (success/error color) before input is accepted again.</summary>
    [SerializeField] private float feedbackDuration = 2f;

    private string currentInput = "";
    private int attemptCount = 0;
    private float feedbackTimer = 0f;
    private bool isWaitingForFeedback = false;

    /// <summary>Invoked when the player enters the correct code.</summary>
    public System.Action onSuccess;
    /// <summary>Invoked when attempts run out (only relevant if <see cref="haveLimitedAttempts"/> is set via <see cref="Init"/>).</summary>
    public System.Action onFailure;

    private void Start()
    {
        // Setup number buttons (0-9)
        for (int i = 0; i < 10; i++)
        {
            int buttonNumber = i; // Local copy for closure
            if (numberButtons[i] != null)
            {
                numberButtons[i].onClick.AddListener(() => OnNumberPressed(buttonNumber));
            }
            else
            {
                Debug.LogWarning($"CodeMachineHandler: Number button {i} is not assigned.");
            }
        }
        
        // Setup clear button
        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClearPressed);
        }
        
        // Setup submit button
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmitPressed);
        }
        
        UpdateDisplay();
    }

    /// <summary>Configures the machine for a new puzzle instance and resets its input state.</summary>
    public void Init(string _correctCode, bool _haveLimitedAttempts = true, int _maxAttempts = 3, System.Action onSuccess = null)
    {
        correctCode = _correctCode;
        haveLimitedAttempts = _haveLimitedAttempts;
        maxAttempts = _maxAttempts;

        this.onSuccess = onSuccess;
        Reset();
    }

    private void OnEnable()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        if (cancelAction != null) cancelAction.action.Enable();
#endif
    }

    private void OnDisable()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        if (cancelAction != null) cancelAction.action.Disable();
#endif
    }

    private void Update()
    {
        if (isWaitingForFeedback)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f)
            {
                isWaitingForFeedback = false;
            }
        }

#if UNITY_ANDROID || UNITY_IOS
        if (Input.GetKeyDown(KeyCode.Escape))
#else
        if (cancelAction != null && cancelAction.action.WasPressedThisFrame())
#endif
        {
            Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.CODEMACHINE);
        }
    }

    private void OnNumberPressed(int number)
    {
        if (isWaitingForFeedback)
            return;

        Toolbox.Soundmanager.PlaySound(Toolbox.Soundmanager.buttonPress);

        currentInput += number.ToString();

        UpdateDisplay();
        
        if (feedbackImage != null)
        {
            feedbackImage.color = defaultColor;
        }

        if (currentInput.Length >= correctCode.Length)
        {
            OnSubmitPressed();
        }
    }

    private void OnClearPressed()
    {
        if (isWaitingForFeedback)
            return;

        Toolbox.Soundmanager.PlaySound(Toolbox.Soundmanager.pressBack);

        currentInput = "";
        UpdateDisplay();
    }

    private void OnSubmitPressed()
    {
        if (isWaitingForFeedback)
            return;
        
        if (currentInput == correctCode)
        {
            ShowFeedback("Code Correct!", successColor);
            OnCodeSuccess();
        }
        else
        {
            if (haveLimitedAttempts)
                attemptCount++;

            int remainingAttempts = maxAttempts - attemptCount;
            if (remainingAttempts > 0)
            {
                ShowFeedback($"Incorrect! {remainingAttempts} attempts remaining.", errorColor);
            }
            else
            {
                ShowFeedback("No attempts remaining. Machine locked.", errorColor);
                OnCodeFailure();
            }
        }
        
        currentInput = "";
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (codeDisplay != null)
        {
            if (string.IsNullOrEmpty(currentInput))
            {
                codeDisplay.text = new string('-', correctCode.Length);
            }
            else
            {
                int remainingDashes = correctCode.Length - currentInput.Length;
                codeDisplay.text = new string('*', currentInput.Length) + new string('-', remainingDashes);
            }
        }
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackImage != null)
        {
            feedbackImage.color = color;
            isWaitingForFeedback = true;
            feedbackTimer = feedbackDuration;
        }

        if (color == successColor && successSound != null)
        {
            Toolbox.Soundmanager.PlaySound(successSound);
        }
        else if (color == errorColor && errorSound != null)
        {
            Toolbox.Soundmanager.PlaySound(errorSound);
        }
    }

    private void OnCodeSuccess()
    {
        onSuccess?.Invoke();
        Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.CODEMACHINE);
    }

    private void OnCodeFailure()
    {
        onFailure?.Invoke();
    }

    /// <summary>
    /// Reset the code machine for another attempt.
    /// </summary>
    public void Reset()
    {
        currentInput = "";
        attemptCount = 0;
        if (feedbackImage != null)
        {
            feedbackImage.color = defaultColor;
        }

        UpdateDisplay();
    }

    /// <summary>
    /// Set a new correct code.
    /// </summary>
    public void SetCorrectCode(string newCode)
    {
        correctCode = newCode;
    }
}
}
