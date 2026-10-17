using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>Simple popup that displays a single message string.</summary>
[MovedFrom(true, null, null, "NotificationListener")]
public class NotificationListener : MonoBehaviour
{
    /// <summary>Text component the message is written to.</summary>
    public TextMeshProUGUI messageTxt;

    /// <summary>Shows the notification with the given message.</summary>
    public void Show(string _msg)
    {
        UpdateTxt(_msg);
    }


    /// <summary>Sets the message text.</summary>
    public void UpdateTxt(string _msg) {

        messageTxt.text = _msg;
    }
}
}
