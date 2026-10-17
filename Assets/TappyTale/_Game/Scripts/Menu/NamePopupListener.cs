using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Popup for entering and saving the player's display name (stored in PlayerPrefs under
/// "playerName"). Wired up as one of <see cref="Toolbox.UiManager"/>'s popups.
/// </summary>
[MovedFrom(true, null, null, "NamePopupListener")]
public class NamePopupListener : MonoBehaviour
{
    /// <summary>Input field the player types their name into.</summary>
    public TMP_InputField nameInput;
    /// <summary>Confirm button; disabled while the input is empty or whitespace-only.</summary>
    public Button okButton;

    void OnEnable()
    {
        nameInput.text = PlayerPrefs.GetString("playerName", "");
        nameInput.onValueChanged.AddListener(OnInputChanged);
        RefreshOkButton();

        nameInput.Select();
        nameInput.ActivateInputField();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nameInput.gameObject);
    }

    void OnDisable()
    {
        nameInput.onValueChanged.RemoveListener(OnInputChanged);
    }

    void OnInputChanged(string _)
    {
        RefreshOkButton();
    }

    void RefreshOkButton()
    {
        if (okButton != null)
            okButton.interactable = !string.IsNullOrWhiteSpace(nameInput.text);
    }

    /// <summary>Saves the trimmed name to PlayerPrefs and closes the popup.</summary>
    public void Press_OK()
    {
        string trimmed = nameInput.text.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        PlayerPrefs.SetString("playerName", trimmed);
        Toolbox.UiManager.popups.GoBack();
    }

    /// <summary>Closes the popup without saving.</summary>
    public void Press_Cancel()
    {
        Toolbox.UiManager.popups.GoBack();
    }
}
}
