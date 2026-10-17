using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>Popup overlay identifiers. Enum order must match the child order under <see cref="UIPopupManager.popupsParent"/>.</summary>
public enum UIPopupList
{
    PAUSE,
    MESSAGEBOX,
    NOTIFICATION,
    OBJECTINSPECTOR,
    CODEMACHINE,
    SAVENOTIFICATION,
}

/// <summary>
/// Drives popup overlays (pause menu, message boxes, code machine, etc.) independently
/// of <see cref="UiManager"/>'s full-screen menu stack. Popups flagged in
/// <see cref="uiModeMenu"/> switch the game into <see cref="PlayMode.UIMODE"/> while shown.
/// </summary>
[MovedFrom(true, null, null, "UIPopupManager")]
public class UIPopupManager : MonoBehaviour
{
    /// <summary>CanvasGroups under <see cref="popupsParent"/>, indexed to match <see cref="UIPopupList"/> values.</summary>
    public List<CanvasGroup> popups;
    /// <summary>Stack of previously shown popups, most recent last. Used by <see cref="GoBack"/>.</summary>
    public List<UIPopupList> history;
    /// <summary>Popups that should pause gameplay and block the underlying menu while shown.</summary>
    public List<UIPopupList> uiModeMenu;

    /// <summary>Parent transform whose children are the popup CanvasGroups, in <see cref="UIPopupList"/> order.</summary>
    public Transform popupsParent;

    public MessageListener msgBox;
    public ObjectInspectorListener objectInspector;
    public CodeMachineListener codeMachine;


    /// <summary>Populates <see cref="popups"/> from <see cref="popupsParent"/>'s children.</summary>
    public void Init()
    {
        popups.Clear();

        for (int i = 0; i < popupsParent.childCount; i++)
        {
            popups.Add(popupsParent.GetChild(i).GetComponent<CanvasGroup>());
        }
    }

    /// <summary>Shows the given popup (hiding all other tracked popups) and pushes it onto <see cref="history"/>.</summary>
    public void EnablePopup(UIPopupList _popup)
    {
        int _nextMenu = (int)_popup;

        CanvasGroup enableMenu = popups[_nextMenu];

        foreach (var item in popups)
        {
            if (item == enableMenu)
            {
                item.gameObject.SetActive(true);

                item.alpha = 1;
                item.interactable = true;
                item.blocksRaycasts = true;

                item.gameObject.SetActive(true);
            }
            else
            {
                item.gameObject.SetActive(false);
                item.alpha = 0;
                item.interactable = false;
                item.blocksRaycasts = false;

                item.gameObject.SetActive(false);
            }
        }

        // Avoid duplicate consecutive entries in the history stack.
        if (!(history.Count > 0 && history[^1] == _popup))
            history.Add(_popup);

        if (uiModeMenu.Contains(_popup))
        {
            GameManager.playMode = PlayMode.UIMODE;
            Toolbox.UiManager.DisableActiveMenuInteraction();
        }
    }

    /// <summary>Shows a popup without affecting <see cref="history"/> or hiding other popups (used for overlays like the code machine).</summary>
    public void EnableIndependentPopup(UIPopupList _popup)
    {
        int val = (int)_popup;

        popups[val].alpha = 1;
        popups[val].interactable = true;
        popups[val].blocksRaycasts = true;

        popups[val].gameObject.SetActive(true);

        if (uiModeMenu.Contains(_popup))
        {
            GameManager.playMode = PlayMode.UIMODE;
            Toolbox.UiManager.DisableActiveMenuInteraction();
        }
    }

    /// <summary>Hides a popup shown via <see cref="EnableIndependentPopup"/> and restores gameplay mode if the HUD is the active menu.</summary>
    public void DisableIndependentPopup(UIPopupList _popup)
    {
        int val = (int)_popup;
        

        popups[val].gameObject.SetActive(false);
        popups[val].alpha = 0;
        popups[val].interactable = false;
        popups[val].blocksRaycasts = false;

        popups[val].gameObject.SetActive(false);

        if (uiModeMenu.Contains(_popup) && Toolbox.UiManager.curShowingMenu == UIList.HUD)
        {
            GameManager.playMode = PlayMode.GAMEMODE;
        }

        if (uiModeMenu.Contains(_popup))
            Toolbox.UiManager.EnableActiveMenuInteraction();
    }

    /// <summary>Coroutine variant of <see cref="DisableIndependentPopup(UIPopupList)"/> that waits <paramref name="_delay"/> seconds first (used by <see cref="ShowSaveNotification"/>).</summary>
    IEnumerator DisableIndependentPopup(UIPopupList _popup, float _delay)
    {
        yield return new WaitForSeconds(_delay);
        
        int val = (int)_popup;

        popups[val].gameObject.SetActive(false);
        popups[val].alpha = 0;
        popups[val].interactable = false;
        popups[val].blocksRaycasts = false;

        popups[val].gameObject.SetActive(false);

        if (uiModeMenu.Contains(_popup) && Toolbox.UiManager.curShowingMenu == UIList.HUD)
        {
            GameManager.playMode = PlayMode.GAMEMODE;
        }

        if (uiModeMenu.Contains(_popup))
            Toolbox.UiManager.EnableActiveMenuInteraction();
    }

    /// <summary>Pops the current popup off <see cref="history"/> and shows the previous one, or hides all popups if none remain.</summary>
    public void GoBack()
    {
        if (history.Count > 1)
        {
            if (uiModeMenu.Contains(history[history.Count - 1]) && Toolbox.UiManager.curShowingMenu == UIList.HUD)
            {            
                GameManager.playMode = PlayMode.GAMEMODE;
            }  

            history.RemoveAt(history.Count - 1);

            EnablePopup(history[history.Count - 1]);
        }
        else {

            history.RemoveAt(history.Count - 1);
            DisableAll();
        }
    }

    void DisableAll() {

        foreach (var item in popups)
        {
            item.alpha = 0;
            item.interactable = false;
            item.blocksRaycasts = false;
            item.gameObject.SetActive(false);
        }

        if (Toolbox.UiManager.curShowingMenu == UIList.HUD)
        {
            GameManager.playMode = PlayMode.GAMEMODE;
        }

        Toolbox.UiManager.EnableActiveMenuInteraction();
    }


    /// <summary>Shows the message box popup with the given title and body text.</summary>
    public void ShowMessageBox(string _title, string _msg)
    {
        EnableIndependentPopup(UIPopupList.MESSAGEBOX);
        msgBox.Show( _title, _msg);
    }

    /// <summary>Shows the object inspector popup focused on the given GameObject.</summary>
    public void ShowObjectInspector(GameObject _obj)
    {
        EnableIndependentPopup(UIPopupList.OBJECTINSPECTOR);
        objectInspector.SetObject(_obj);
    }

    /// <summary>Shows the code machine popup configured with the given correct code and attempt limit.</summary>
    public void ShowCodeMachine(string _correctCode, bool _haveLimitedAttempts, int _maxAttempts, System.Action onSuccess)
    {
        EnableIndependentPopup(UIPopupList.CODEMACHINE);
        codeMachine.Init(_correctCode, _haveLimitedAttempts, _maxAttempts, onSuccess);
    }

    /// <summary>Shows the save notification popup for a fixed 4-second duration.</summary>
    public void ShowSaveNotification()
    {
        EnableIndependentPopup(UIPopupList.SAVENOTIFICATION);
        StartCoroutine(DisableIndependentPopup(UIPopupList.SAVENOTIFICATION, 4f));
    }

    // Editor-only helpers used by the custom UIPopupManager inspector (Editor/PopupInspector.cs)
    // to preview popups in edit mode, bypassing the runtime history stack.
    #region Editor Functions

    /// <summary>Editor-only: shows a popup by index without touching <see cref="history"/>.</summary>
    public void EnablePopupFromEditor(int _nextMenu)
    {
        CanvasGroup enableMenu = popups[_nextMenu];

        foreach (var item in popups)
        {
            if (item == enableMenu)
            {
                item.gameObject.SetActive(true);
                item.alpha = 1;
                item.interactable = true;
                item.blocksRaycasts = true;
                item.gameObject.SetActive(true);
            }
            else
            {
                item.gameObject.SetActive(false);
                item.alpha = 0;
                item.interactable = false;
                item.blocksRaycasts = false;
                item.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Editor-only: shows a popup by index and selects its GameObject in the Hierarchy.</summary>
    public void SelectMenuFromEditor(int _nextMenu)
    {
        CanvasGroup enableMenu = popups[_nextMenu];
#if UNITY_EDITOR
        Selection.activeGameObject = enableMenu.gameObject;
#endif

        foreach (var item in popups)
        {
            if (item == enableMenu)
            {
                item.gameObject.SetActive(true);
                item.alpha = 1;
                item.interactable = true;
                item.blocksRaycasts = true;
                item.gameObject.SetActive(true);
            }
            else
            {
                item.gameObject.SetActive(false);
                item.alpha = 0;
                item.interactable = false;
                item.blocksRaycasts = false;
                item.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Editor-only: hides all tracked popups.</summary>
    public void DisableAllFromEditor()
    {
        foreach (var item in popups)
        {
            item.gameObject.SetActive(false);
            item.alpha = 0;
            item.interactable = false;
            item.blocksRaycasts = false;            
            item.gameObject.SetActive(false);
        }
    }

    #endregion
}
}