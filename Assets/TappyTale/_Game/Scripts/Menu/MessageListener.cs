using System;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>Message box popup: shows a title and body message, closable via a button or Escape.</summary>
[MovedFrom(true, null, null, "MessageListener")]
public class MessageListener : MonoBehaviour
{
    /// <summary>Title text component.</summary>
    public TextMeshProUGUI titleTxt;
    /// <summary>Body message text component.</summary>
    public TextMeshProUGUI messageTxt;

    /// <summary>Shows the message box with the given title and body text.</summary>
    public void Show(string _title, string _msg)
    {
        UpdateTxt(_title, _msg);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    /// <summary>Sets the title and body text without changing popup visibility.</summary>
    public void UpdateTxt(string _titleTxt, string _msg) {

        titleTxt.text = _titleTxt;
        messageTxt.text = _msg;
    }

    /// <summary>Closes the message box, returning to the previous popup (or hiding this one if there is no history).</summary>
    public void Close() {

        Toolbox.Soundmanager.PlaySound(Toolbox.Soundmanager.buttonPress);

        try
        {
            Toolbox.UiManager.popups.GoBack();
        }
        catch (ArgumentOutOfRangeException)
        {
            // GoBack() indexes into an empty popup history; fall back to just closing this popup.
            Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.MESSAGEBOX);
        }
    }
}
}
