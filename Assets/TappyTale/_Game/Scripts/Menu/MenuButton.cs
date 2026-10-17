using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting.APIUpdating;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>
/// Generic menu button: plays a click sound and invokes one of the <c>Press_*</c> methods
/// below, selected via a dropdown in the custom Inspector (<see cref="MenuButtonEditor"/>).
/// In edit mode it can also auto-sync its GameObject name and child label text to
/// <see cref="btnName"/>.
/// </summary>
[ExecuteInEditMode]
[MovedFrom(true, null, null, "MenuButton")]
public class MenuButton : MonoBehaviour
{
    [Header("Quick Action")]
    /// <summary>Name of the public <c>Press_*</c> method to invoke on click, set via the custom Inspector dropdown.</summary>
    [HideInInspector] public string selectedAction;

    [Header("Text")]
    public string btnName;
    /// <summary>If true, the GameObject name and child TextMeshPro label are kept in sync with <see cref="btnName"/> in the editor.</summary>
    public bool autoUpdateTxt = true;

    [Header("Selection")]
    /// <summary>If true, this button becomes the selected EventSystem object when enabled.</summary>
    public bool selectOnEnable;

    [Header("Sound")]
    [SerializeField] private bool hasSound = true;
    [Tooltip("If left alone. It will have button sound")]
    [SerializeField] private AudioClip uniquePressSound;

    void OnEnable()
    {
        if (selectOnEnable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

#if UNITY_EDITOR
    private string lastBtnName;

    private void OnValidate()
    {
        EditorApplication.delayCall += UpdateButtonName;
    }

    private void UpdateButtonName()
    {
        if (this == null) return;

        if (!autoUpdateTxt)
            return;

        if (lastBtnName != btnName && !string.IsNullOrEmpty(btnName))
        {
            lastBtnName = btnName;
            gameObject.name = btnName;

            if (transform.childCount > 0)
            {
                var tmp = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = btnName;
                    EditorUtility.SetDirty(tmp);
                }
            }

            EditorUtility.SetDirty(gameObject);
        }
    }
#endif

    private void Start()
    {
        if (!Application.isPlaying) return;

        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() =>
            {
                ButtonSound();
                InvokeSelectedAction();
            });
        }
    }

    private void InvokeSelectedAction()
    {
        if (string.IsNullOrEmpty(selectedAction) || selectedAction == "None") return;

        // selectedAction is a public, parameterless Press_* method name chosen in the custom Inspector.
        var method = GetType().GetMethod(selectedAction, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method?.Invoke(this, null);
    }

    /// <summary>Plays <see cref="uniquePressSound"/> if set, otherwise the default button-press sound.</summary>
    public void ButtonSound()
    {
        if (!hasSound) return;

        if (uniquePressSound)
        {
            Toolbox.Soundmanager.PlaySound(uniquePressSound);
        }
        else
        {
            Toolbox.Soundmanager.PlaySound(Toolbox.Soundmanager.buttonPress);
        }
    }


    #region Common
    /// <summary>Opens the pause popup and freezes gameplay time.</summary>
    public void Press_Common_Pause()
    {
        Toolbox.UiManager.popups.EnablePopup(UIPopupList.PAUSE);
        Toolbox.Soundmanager.Pause_All();
        Time.timeScale = 0f;
    }

    /// <summary>Clears all PlayerPrefs and reloads the active scene.</summary>
    public void Press_Common_ResetProgress()
    {
        Time.timeScale = 1f;

        PlayerPrefs.DeleteAll();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }


    /// <summary>Clears all PlayerPrefs and reloads the active scene (used to return from the credits screen).</summary>
    public void Press_Common_CreditsBack()
    {
        PlayerPrefs.DeleteAll();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    #endregion

    #region Main Menu
    /// <summary>Switches to the HUD menu and enters gameplay mode.</summary>
    public void Press_MainMenu_Play()
    {
        Toolbox.UiManager.EnableMenu(UIList.HUD);

        GameManager.playMode = PlayMode.GAMEMODE;
    }




    /// <summary>Quits the application (stops Play mode instead, when run in the editor).</summary>
    public void Press_MainMenu_Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    #endregion

    #region Fail

    /// <summary>Reloads the active scene after a short delay.</summary>
    public void Press_Fail_Restart()
    {
        Invoke(nameof(ReloadScene), 2f);
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    #endregion


    #region Popups
    /// <summary>Closes the pause popup and resumes gameplay time.</summary>
    public void Press_Pause_Resume()
    {
        Toolbox.UiManager.popups.GoBack();
        Toolbox.Soundmanager.UnPause_All();
        Time.timeScale = 1f;
    }

    /// <summary>Closes the pause popup, resumes time, and reloads the scene after a short delay.</summary>
    public void Press_Pause_RestartFromLastCheckpoint()
    {
        Time.timeScale = 1f;
        Toolbox.Soundmanager.UnPause_All();
        Toolbox.UiManager.popups.GoBack();
        Invoke(nameof(ReloadScene), 0.5f);
    }

    /// <summary>Closes the message box popup.</summary>
    public void Press_Msg_Close()
    {
        Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.MESSAGEBOX);
    }

    /// <summary>Closes the object inspector popup.</summary>
    public void Press_ObjectInspector_Close()
    {
        Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.OBJECTINSPECTOR);
    }

    /// <summary>Closes the code machine popup.</summary>
    public void Press_CodeMachine_Close()
    {
        Toolbox.UiManager.popups.DisableIndependentPopup(UIPopupList.CODEMACHINE);
    }
    #endregion
}

#if UNITY_EDITOR
/// <summary>Custom Inspector for <see cref="MenuButton"/> that exposes <see cref="MenuButton.selectedAction"/> as a dropdown of its public parameterless <c>Press_*</c> methods.</summary>
[UnityEditor.CustomEditor(typeof(MenuButton))]
[MovedFrom(true, null, null, "MenuButtonEditor")]
public class MenuButtonEditor : UnityEditor.Editor
{
    private string[] actionOptions;
    private int selectedIndex = 0;

    private void OnEnable()
    {
        RefreshActionOptions();
    }

    private void RefreshActionOptions()
    {
        var methods = typeof(MenuButton)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Press_") && m.GetParameters().Length == 0)
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToList();

        methods.Insert(0, "None");
        actionOptions = methods.ToArray();

        var menuButton = (MenuButton)target;
        if (!string.IsNullOrEmpty(menuButton.selectedAction))
        {
            selectedIndex = System.Array.IndexOf(actionOptions, menuButton.selectedAction);
            if (selectedIndex < 0) selectedIndex = 0;
        }
    }

    public override void OnInspectorGUI()
    {
        var menuButton = (MenuButton)target;

        UnityEditor.EditorGUILayout.Space(5);
        UnityEditor.EditorGUILayout.LabelField("Quick Action", UnityEditor.EditorStyles.boldLabel);

        UnityEditor.EditorGUI.BeginChangeCheck();
        selectedIndex = UnityEditor.EditorGUILayout.Popup("Button Action", selectedIndex, actionOptions);

        if (UnityEditor.EditorGUI.EndChangeCheck())
        {
            UnityEditor.Undo.RecordObject(menuButton, "Change Button Action");
            menuButton.selectedAction = actionOptions[selectedIndex];
            UnityEditor.EditorUtility.SetDirty(menuButton);
        }

        UnityEditor.EditorGUILayout.Space(10);
        DrawDefaultInspector();
    }
}
#endif
}
