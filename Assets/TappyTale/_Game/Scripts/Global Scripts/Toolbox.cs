using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Service locator for the game's global manager singletons. Attach alongside
/// <see cref="GameManager"/> and <see cref="SoundManager"/> on a single persistent
/// GameObject; any script can then reach them via <see cref="Toolbox.GameManager"/>,
/// <see cref="Toolbox.Soundmanager"/> and <see cref="Toolbox.UiManager"/> without
/// needing a direct reference.
/// </summary>
[RequireComponent(typeof(GameManager))]
[RequireComponent(typeof(SoundManager))]
[MovedFrom(true, null, null, "Toolbox")]
public class Toolbox : MonoBehaviour {
    private static GameManager gameManager;
    private static SoundManager soundManager;

    private static UiManager uiManager;

    /// <summary>Global game/play-mode state manager.</summary>
    public static GameManager GameManager {
        get { return gameManager; }
    }

    /// <summary>Global audio playback manager.</summary>
    public static SoundManager Soundmanager {
        get { return soundManager; }
    }

    /// <summary>Global menu/UI state manager. Assigned by <see cref="UiManager"/> itself on enable.</summary>
    public static UiManager UiManager
    {
        get { return uiManager; }
    }

    void Awake()
    {
        EnsureManagersAssigned();
    }

    void OnEnable()
    {
        EnsureManagersAssigned();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureManagersAssigned();
    }
#endif

    private void EnsureManagersAssigned()
    {
        if (gameManager == null)
            gameManager = GetComponent<GameManager>();

        if (soundManager == null)
            soundManager = GetComponent<SoundManager>();
    }

    /// <summary>Registers the active <see cref="UiManager"/> instance. Called by <see cref="UiManager.OnEnable"/>.</summary>
    public static void Set_UIManager(UiManager _uiManager)
    {
        uiManager = _uiManager;
    }

}
}