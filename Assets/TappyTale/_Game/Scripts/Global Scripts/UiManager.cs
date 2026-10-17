using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>Full-screen menu identifiers. Enum order must match the child order under <see cref="UiManager.menuParent"/>.</summary>
public enum UIList {

    HUD,
    FAIL,

}

/// <summary>
/// Drives the full-screen menu stack (HUD, fail screen, etc.), tracking a history so
/// <see cref="GoBack"/> can return to the previous menu. Popup overlays are handled
/// separately by <see cref="popups"/>.
/// </summary>
[MovedFrom(true, null, null, "UiManager")]
public class UiManager : MonoBehaviour
{

    public GameObject bg;

    /// <summary>CanvasGroups under <see cref="menuParent"/>, indexed to match <see cref="UIList"/> values.</summary>
    public List<CanvasGroup> menues;

    /// <summary>Stack of previously shown menus, most recent last.</summary>
    public List<UIList> history;

    /// <summary>The menu currently on top of <see cref="history"/>, or <see cref="UIList.HUD"/> if empty.</summary>
    public UIList curShowingMenu { get {

            if (history.Count > 0)
                return history[^1];
            else
                return UIList.HUD;
        }
    }

    [Space(10)]
    /// <summary>Menus that should keep the ad banner visible while shown; all others hide it.</summary>
    public UIList[] showBannerUiList;
    /// <summary>Menus that should show <see cref="bg"/> while shown; all others hide it.</summary>
    public UIList[] showBgUiList;

    [Space(10)]
    /// <summary>Parent transform whose children are the menu CanvasGroups, in <see cref="UIList"/> order.</summary>
    public Transform menuParent;
    public UIPopupManager popups;
    [Space(5)]
    public HUDListener hud;
    public FadeImg blackout;


    private void OnEnable()
    {
        Toolbox.Set_UIManager(this);
    }

    /// <summary>Populates <see cref="menues"/> from <see cref="menuParent"/>'s children and resolves <see cref="popups"/>/<see cref="hud"/> if unassigned.</summary>
    public void Init()
    {
        menues.Clear();

        for (int i = 0; i < menuParent.childCount; i++)
        {
            menues.Add(menuParent.GetChild(i).GetComponent<CanvasGroup>());
        }

        if (popups == null) popups = GetComponentInChildren<UIPopupManager>();

        if (hud == null) hud = GetComponentInChildren<HUDListener>();
    }

    /// <summary>Shows the given menu (hiding all others) and pushes it onto <see cref="history"/>.</summary>
    public void EnableMenu(UIList _menu)
    {
        int _nextMenu = (int)_menu;

        if (_menu == UIList.HUD && blackout)
            blackout.StartFadeOut();

        CanvasGroup enableMenu = menues[_nextMenu];

        foreach (var item in menues)
        {
            if(item == null) continue;

            if (item == enableMenu)
            {
                item.gameObject.SetActive(true);
                item.alpha = 1;
                item.interactable = true;
                item.blocksRaycasts = true;                    
            }
            else
            {
                item.gameObject.SetActive(false);
                item.alpha = 0;
                item.interactable = false;
                item.blocksRaycasts = false;
            }
        }

        // Avoid duplicate consecutive entries in the history stack.
        if (!(history.Count > 0 && history[^1] == _menu))
            history.Add(_menu);

        if (bg)
            ShowBGHandling(_menu);

        ShowBannerHandling(_menu);
    }

    /// <summary>Shows or hides the entire menu root (<see cref="menuParent"/>).</summary>
    public void UIStatus(bool _val) {

        menuParent.gameObject.SetActive(_val);
    }

    /// <summary>Disables interaction/raycasts and hides the currently shown menu without altering history.</summary>
    public void DisableActiveMenuInteraction()
    {
        int idx = (int)curShowingMenu;
        if (idx < menues.Count)
        {
            menues[idx].alpha = 0;
            menues[idx].interactable = false;
            menues[idx].blocksRaycasts = false;
            menues[idx].gameObject.SetActive(false);
        }
    }

    /// <summary>Re-enables interaction/raycasts and shows the currently tracked menu (the inverse of <see cref="DisableActiveMenuInteraction"/>).</summary>
    public void EnableActiveMenuInteraction()
    {
        int idx = (int)curShowingMenu;
        if (idx < menues.Count)
        {
            menues[idx].alpha = 1;
            menues[idx].interactable = true;
            menues[idx].blocksRaycasts = true;
            menues[idx].gameObject.SetActive(true);
        }
    }

    /// <summary>Pops the current menu off <see cref="history"/> and shows the previous one.</summary>
    public void GoBack()
    {

        if (history.Count > 1)
        {
            history.RemoveAt(history.Count - 1);

            EnableMenu(history[^1]);
        }
    }

    /// <summary>Shows a menu by its <see cref="UIList"/> int value (for UnityEvent bindings that can't reference the enum directly).</summary>
    public void EnableMenuByValue(int _val) {

        switch (_val)
        {
            case 0:
                EnableMenu(UIList.HUD); break;

            case 1:
                EnableMenu(UIList.FAIL); break;

            default:
                break;
        }
    }

    /// <summary>Shows the fail screen after a delay.</summary>
    public void ShowFailScreen(float _delay) {

        StartCoroutine(CR_ShowFailScreen(_delay));
    }

    IEnumerator CR_ShowFailScreen(float _delay) {

        yield return new WaitForSeconds(_delay);

        EnableMenu(UIList.FAIL);
    }

    #region Extra

    /// <summary>Hides the ad banner unless <paramref name="_menu"/> is in <see cref="showBannerUiList"/>. Requires the ADMOB_ADS scripting define.</summary>
    public void ShowBannerHandling(UIList _menu)
    {
        foreach (var item in showBannerUiList)
        {
            if (item == _menu)
                return;
        }

#if ADMOB_ADS
        AdsManager.instance.HideBanner();
#endif
    }

    /// <summary>Shows or hides <see cref="bg"/> depending on whether <paramref name="_menu"/> is in <see cref="showBgUiList"/>.</summary>
    public void ShowBGHandling(UIList _menu)
    {

        foreach (var item in showBgUiList)
        {
            if (item == _menu)
            {
                bg.SetActive(true);
                return;
            }
        }

        bg.SetActive(false);
    }

    #endregion

    // Editor-only helpers used by the custom UiManager inspector (Editor/UIInspector.cs)
    // to preview menus in edit mode, bypassing the runtime history stack.
    #region EditorFunctions

    /// <summary>Editor-only: shows a menu by index without touching <see cref="history"/>.</summary>
    public void EnableMenuFromEditor(int _nextMenu)
    {
        CanvasGroup enableMenu = menues[_nextMenu];

        foreach (var item in menues)
        {
            if (item == enableMenu)
            {

                item.gameObject.SetActive(true);
                item.alpha = 1;
                item.interactable = true;
                item.blocksRaycasts = true;
            }
            else
            {
                item.gameObject.SetActive(false);
                item.alpha = 0;
                item.interactable = false;
                item.blocksRaycasts = false;
            }
        }
    }

    /// <summary>Editor-only: shows a menu by index and selects its GameObject in the Hierarchy.</summary>
    public void SelectMenuFromEditor(int _nextMenu)
    {
        CanvasGroup enableMenu = menues[_nextMenu];
#if UNITY_EDITOR
        Selection.activeGameObject = enableMenu.gameObject;
#endif

        foreach (var item in menues)
        {
            if (item == enableMenu)
            {

                item.gameObject.SetActive(true);
                item.alpha = 1;
                item.interactable = true;
                item.blocksRaycasts = true;
            }
            else
            {
                item.gameObject.SetActive(false);
                item.alpha = 0;
                item.interactable = false;
                item.blocksRaycasts = false;
            }
        }
    }

    #endregion
}
}
