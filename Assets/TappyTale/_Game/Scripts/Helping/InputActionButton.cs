using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Lets an input action (e.g. a gamepad/keyboard binding) trigger a UI Button's onClick, in addition
/// to normal pointer clicks. Supports up to two bound actions (e.g. a primary and a controller-specific alt binding).
/// </summary>
[RequireComponent(typeof(Button))]
[MovedFrom(true, null, null, "InputActionButton")]
public class InputActionButton : MonoBehaviour
{
    public InputActionReference inputAction;
    public InputActionReference inputAction2;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>If true (and on Android/iOS), pressing the device Escape/Back key also triggers this button.</summary>
    public bool isCancelAction;

    void OnEnable()
    {
        if (inputAction != null)
        {
            inputAction.action.performed += OnActionPerformed;
            inputAction.action.Enable();
        }
        if (inputAction2 != null)
        {
            inputAction2.action.performed += OnActionPerformed;
            inputAction2.action.Enable();
        }
    }

    void OnDisable()
    {
        if (inputAction != null)
        {
            inputAction.action.performed -= OnActionPerformed;
            inputAction.action.Disable();
        }
        if (inputAction2 != null)
        {
            inputAction2.action.performed -= OnActionPerformed;
            inputAction2.action.Disable();
        }
    }

#if UNITY_ANDROID || UNITY_IOS
    void Update()
    {
        if (isCancelAction && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TryPress();
    }
#endif

    void OnActionPerformed(InputAction.CallbackContext ctx) => TryPress();

    void TryPress()
    {
        if (button.interactable)
            button.onClick.Invoke();
    }
}
}
