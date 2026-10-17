using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
    /// <summary>
    /// Drives the gameplay HUD: interact/drop prompts, mobile control visibility, and the
    /// Escape key shortcut for opening the pause menu / closing the active popup. Expects a
    /// <see cref="PlayerHandler"/> to exist in the scene.
    /// </summary>
    [MovedFrom(true, null, null, "HUDListener")]
    public class HUDListener : MonoBehaviour
    {
        private PlayerHandler player;

        [Space(5)]
        public TextMeshProUGUI interactMsg;

        [Header("Control Ref")]
        public GameObject interactRef;
        public GameObject dropRef;

        [Header("Control Ref (Mobile)")]
        public GameObject interactRefMobile;
        public GameObject dropRefMobile;

        [Space(10)]
        /// <summary>Whether the on-screen interact/drop controls are currently visible.</summary>
        public bool showControls = true;

        [Space(10)]
        /// <summary>Input action that opens the pause menu / closes the active popup.</summary>
        public InputActionReference escape;

        public void Awake()
        {
            if (escape != null) escape.action.Enable();
            player = FindAnyObjectByType<PlayerHandler>();
        }

        void Update()
        {
#if UNITY_ANDROID || UNITY_IOS
        bool cancelPressed = Input.GetKeyDown(KeyCode.Escape);
#else
            bool cancelPressed = escape != null && escape.action.WasPressedThisFrame();
#endif
            if (cancelPressed)
            {
                if (GameManager.playMode == PlayMode.GAMEMODE)
                {
                    Toolbox.UiManager.popups.EnablePopup(UIPopupList.PAUSE);
                    Toolbox.Soundmanager.Pause_All();
                    Time.timeScale = 0f;

                }
                else if (GameManager.playMode == PlayMode.UIMODE)
                {
                    Time.timeScale = 1;
                }
            }
        }

        /// <summary>Shows the interact prompt with the given message.</summary>
        public void ShowInteractMsg(string _str)
        {

#if UNITY_ANDROID || UNITY_IOS
        if(interactRefMobile)
        {
            interactRefMobile.SetActive(true);
            interactMsg.text = _str;
        }
#else
            if (interactRef)
            {
                interactRef.SetActive(true);
                interactMsg.text = _str;
            }
#endif
        }

        /// <summary>Hides the interact prompt.</summary>
        public void HideInteractMsg()
        {

#if UNITY_ANDROID || UNITY_IOS
        if(interactRefMobile) interactRefMobile.SetActive(false);
#else
            if (interactRef) interactRef.SetActive(false);
#endif
        }


#if UNITY_ANDROID || UNITY_IOS
    /// <summary>Mobile touch-button handler: performs the current interaction.</summary>
    public void PressInteract()
    {
        player.GetComponent<InteractionHandler>().Interact();
    }

    /// <summary>Mobile touch-button handler: drops the currently held item.</summary>
    public void PressDrop()
    {
        player.playerItem.DropItem();
    }
#endif

        /// <summary>Shows or hides the drop control button (e.g. when no item is held).</summary>
        public void DropControlStatus(bool canDrop)
        {

#if UNITY_ANDROID || UNITY_IOS
        if(dropRefMobile) dropRefMobile.SetActive(canDrop);
#else
            if (dropRef) dropRef.SetActive(canDrop);
#endif
        }

        /// <summary>Shows or hides the interact/drop control buttons entirely.</summary>
        public void SetControlsVisibility(bool visible)
        {

            showControls = visible;

#if UNITY_ANDROID || UNITY_IOS
        if(interactRefMobile) interactRefMobile.SetActive(visible);
        if(dropRefMobile) dropRefMobile.SetActive(visible);
#else
            if (interactRef) interactRef.SetActive(visible);
            if (dropRef) dropRef.SetActive(visible);
#endif
        }
    }
}
