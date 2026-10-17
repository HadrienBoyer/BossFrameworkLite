using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// World-placed trigger for a code machine puzzle: opens the shared code machine popup
/// (<see cref="CodeMachineListener"/>) configured with this instance's code/attempt settings.
/// </summary>
[MovedFrom(true, null, null, "CodeMachine")]
public class CodeMachine : MonoBehaviour
{
    /// <summary>The code the player must enter to solve this machine.</summary>
    public string _correctCode;
    /// <summary>If true, the machine locks after <see cref="_maxAttempts"/> failed attempts.</summary>
    public bool _haveLimitedAttempts = true;
    public int _maxAttempts = 3;

    /// <summary>Invoked when the player enters the correct code.</summary>
    public UnityEvent onCorrectCodeEntered;

    /// <summary>Opens the code machine popup configured for this instance.</summary>
    public void OpenCodeMachineUI()
    {
        Toolbox.UiManager.popups.ShowCodeMachine(_correctCode, _haveLimitedAttempts, _maxAttempts, OnCorrectCodeEntered);
    }

    /// <summary>Callback invoked by the code machine popup on success; closes the popup and fires <see cref="onCorrectCodeEntered"/>.</summary>
    public void OnCorrectCodeEntered()
    {
        Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.CODEMACHINE);
        onCorrectCodeEntered?.Invoke();
    }
}
}
