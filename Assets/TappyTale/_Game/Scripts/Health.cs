using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
    /// <summary>
    /// Drives the various ways an object can "die": a triggered animation, a ragdoll swap,
    /// an instant kill, or an explosion. Each death mode is self-contained (sound, component
    /// cleanup, destroy delay, UnityEvent) and can be called independently.
    /// </summary>
    [MovedFrom(true, null, null, "Health")]
    public class Health : MonoBehaviour
    {
        /// <summary>Only used when this GameObject is tagged "Player": the transform a temporary camera target is spawned at on death.</summary>
        public Transform audioListner;

        [Header("Animation Death")]
        public bool animDestroyOnDeath = true;
        public float animDestroyDelay = 1f;
        public Animator animator;
        public string deathAnimationTrigger = "Die";
        public AudioClip animDeathSound;
        [Range(0f, 1f)] public float animDeathVolume = 1f;
        /// <summary>Components destroyed (not just disabled) when this death mode triggers.</summary>
        public Component[] animDisableComponents;
        public UnityEvent onAnimDeath;

        [Header("Ragdoll Death")]
        public bool destroyPlayer = true;
        public float playerDestroyDelay = 1f;
        public CharacterController controller;
        public GameObject ragdoll;
        public GameObject playerMesh;
        public AudioClip ragdollDeathSound;
        [Range(0f, 1f)] public float ragdollDeathVolume = 1f;
        public bool shakeOnRagdollDeath = true;
        public float ragdollDeathShakeIntensity = 100f;
        public float ragdollDeathShakeDuration = 1f;
        /// <summary>Components destroyed (not just disabled) when this death mode triggers.</summary>
        public Component[] ragdollDisableComponents;
        public UnityEvent onRagdollDeath;

        [Header("Explosion Death")]
        public bool explosionDestroyOnDeath = true;
        public float explosionDestroyDelay = 0f;
        public AudioClip explosionDeathSound;
        [Range(0f, 1f)] public float explosionDeathVolume = 1f;
        public float explosionForce = 500f;
        public float explosionRadius = 5f;
        public float explosionUpwardModifier = 1f;
        /// <summary>Transforms detached and pushed by the explosion force (e.g. ragdoll bones), each destroyed after <see cref="explosionDebrisDestroyDelay"/>.</summary>
        public Transform[] explosionAffectedBodies;
        public float explosionDebrisDestroyDelay = 3f;
        public GameObject explosionEffect;
        /// <summary>If true, the explosion also kills other <see cref="Health"/> objects within <see cref="killRadius"/> on <see cref="killLayer"/>.</summary>
        public bool effectsOthersOnExplosion = false;
        public float killRadius = 5f;
        public LayerMask killLayer;
        /// <summary>Components destroyed (not just disabled) when this death mode triggers.</summary>
        public Component[] explosionDisableComponents;
        public UnityEvent onExplosionDeath;

        [Header("Instant Death")]
        public bool instantDestroyOnDeath = true;
        public float instantDestroyDelay = 0f;
        public AudioClip instantDeathSound;
        public bool shakeOnInstantDeath = true;
        public float instantDeathShakeIntensity = 1f;
        public float instantDeathShakeDuration = 1f;
        [Range(0f, 1f)] public float instantDeathVolume = 1f;
        /// <summary>Components destroyed (not just disabled) when this death mode triggers.</summary>
        public Component[] instantDisableComponents;
        public UnityEvent onInstantDeath;

        private void OnPlayerDead()
        {
            if (!CompareTag("Player")) return;

            GetComponent<PlayerHandler>().cameraHandler.SetTarget(Instantiate(new GameObject("CameraTarget"), transform.position, transform.rotation).transform);
            Toolbox.UiManager.ShowFailScreen(1);
        }

        /// <summary>Triggers the death animation, plays its sound, destroys the configured components, and invokes <see cref="onAnimDeath"/>.</summary>
        public void DieWithAnimation()
        {
            foreach (var component in animDisableComponents)
            {
                if (component != null) Destroy(component);
            }

            PlayDeathSound(animDeathSound, animDeathVolume);

            if (animator != null)
                animator.SetTrigger(deathAnimationTrigger);

            OnPlayerDead();

            if (animDestroyOnDeath)
                Destroy(gameObject, animDestroyDelay);

            onAnimDeath?.Invoke();
        }

        /// <summary>Hides the player mesh, spawns the ragdoll (pushed away from <paramref name="attackerTransform"/> by <paramref name="force"/>), plays its sound, and invokes <see cref="onRagdollDeath"/>.</summary>
        public void DieWithRagdoll(Transform attackerTransform, float force = 1f)
        {
            if (playerMesh != null)
                playerMesh.SetActive(false);

            foreach (var component in ragdollDisableComponents)
            {
                if (component != null) Destroy(component);
            }

            if (controller != null)
            {
                controller.detectCollisions = false;
            }

            if (shakeOnRagdollDeath)
                Camera.main.GetComponent<CameraHandler>()?.ShakeCamera(ragdollDeathShakeIntensity, ragdollDeathShakeDuration);

            PlayDeathSound(ragdollDeathSound, ragdollDeathVolume);

            if (ragdoll != null)
                SpawnRagdoll(attackerTransform, force);



            OnPlayerDead();

            if (destroyPlayer)
                Destroy(gameObject, playerDestroyDelay);

            onRagdollDeath?.Invoke();
        }

        /// <summary>Kills the object immediately: destroys the configured components, plays its sound, shakes the camera, and invokes <see cref="onInstantDeath"/>.</summary>
        public void InstantDie()
        {
            foreach (var component in instantDisableComponents)
            {
                if (component != null) Destroy(component);
            }

            PlayDeathSound(instantDeathSound, instantDeathVolume);

            OnPlayerDead();

            if (instantDestroyOnDeath)
                Destroy(gameObject, instantDestroyDelay);

            onInstantDeath?.Invoke();

            if (shakeOnInstantDeath)
                Camera.main.GetComponent<CameraHandler>()?.ShakeCamera(instantDeathShakeIntensity, instantDeathShakeDuration);

        }

        /// <summary>
        /// Destroys the configured components, spawns the explosion effect, applies explosion force to
        /// <see cref="explosionAffectedBodies"/>, optionally kills nearby <see cref="Health"/> objects
        /// (see <see cref="effectsOthersOnExplosion"/>), and invokes <see cref="onExplosionDeath"/>.
        /// </summary>
        public void DieWithExplosion()
        {
            foreach (var component in explosionDisableComponents)
            {
                if (component != null) Destroy(component);
            }

            PlayDeathSound(explosionDeathSound, explosionDeathVolume);

            if (explosionEffect != null)
                Instantiate(explosionEffect, transform.position, Quaternion.identity);

            foreach (var t in explosionAffectedBodies)
            {
                if (t == null) continue;
                t.SetParent(null);
                if (!t.TryGetComponent(out Rigidbody rb))
                    rb = t.gameObject.AddComponent<Rigidbody>();
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, explosionUpwardModifier, ForceMode.Impulse);
                Destroy(t.gameObject, explosionDebrisDestroyDelay);
            }

            if (effectsOthersOnExplosion)
                KillInRange();

            OnPlayerDead();

            onExplosionDeath?.Invoke();

            if (explosionDestroyOnDeath)
                Destroy(gameObject, explosionDestroyDelay);
        }

        private static readonly Collider[] killBuffer = new Collider[32];

        private void KillInRange()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, killRadius, killBuffer, killLayer);
            for (int i = 0; i < count; i++)
            {
                Collider col = killBuffer[i];
                Health health = col.GetComponent<Health>();
                if (health == null || health == this) continue;

                if (health.ragdoll != null)
                    health.DieWithRagdoll(transform, explosionForce);
                else
                    health.InstantDie();
            }
        }

        private void PlayDeathSound(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            GameObject audioObject = new GameObject($"{name}_DeathAudio");
            audioObject.transform.position = transform.position;

            AudioSource tempAudioSource = audioObject.AddComponent<AudioSource>();
            tempAudioSource.clip = clip;
            tempAudioSource.spatialBlend = 0.8f;
            tempAudioSource.volume = volume;
            tempAudioSource.Play();

            Destroy(audioObject, clip.length);
        }

        private void SpawnRagdoll(Transform attackerTransform, float force)
        {
            GameObject spawnedRagdoll = Instantiate(ragdoll, transform.position, transform.rotation);
            spawnedRagdoll.SetActive(true);

            if (attackerTransform != null)
            {
                Force forceComponent = spawnedRagdoll.GetComponent<Force>();
                if (forceComponent != null)
                {
                    Vector3 forceDirection = (transform.position - attackerTransform.position).normalized;
                    forceDirection.y = 0.5f;
                    forceDirection.Normalize();
                    forceComponent.ApplyForce(forceDirection, force);
                }
            }
        }
    }


#if UNITY_EDITOR
    /// <summary>Custom inspector for <see cref="Health"/>: groups each death mode into a foldout and adds a play-mode test button per mode.</summary>
    [CustomEditor(typeof(Health))]
    [MovedFrom(true, null, null, "HealthEditor")]
    public class HealthEditor : Editor
    {
        private bool showAnimDeath = false;
        private bool showRagdollDeath = false;
        private bool showInstantDeath = false;
        private bool showExplosionDeath = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Health script = (Health)target;

            if (script.CompareTag("Player"))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("audioListner"));

            EditorGUILayout.Space(5);
            showAnimDeath = EditorGUILayout.Foldout(showAnimDeath, "Animation Death", true, EditorStyles.foldoutHeader);
            if (showAnimDeath)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animDestroyOnDeath"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animDestroyDelay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("deathAnimationTrigger"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animDeathSound"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animDeathVolume"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animDisableComponents"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onAnimDeath"));
                if (GUILayout.Button("Test: Die With Animation") && Application.isPlaying)
                    script.DieWithAnimation();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            showRagdollDeath = EditorGUILayout.Foldout(showRagdollDeath, "Ragdoll Death", true, EditorStyles.foldoutHeader);
            if (showRagdollDeath)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyPlayer"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("playerDestroyDelay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdoll"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("playerMesh"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdollDeathSound"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdollDeathVolume"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shakeOnRagdollDeath"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdollDeathShakeIntensity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdollDeathShakeDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ragdollDisableComponents"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onRagdollDeath"));
                if (GUILayout.Button("Test: Die With Ragdoll") && Application.isPlaying)
                    script.DieWithRagdoll(null, 0);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            showInstantDeath = EditorGUILayout.Foldout(showInstantDeath, "Instant Death", true, EditorStyles.foldoutHeader);
            if (showInstantDeath)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDestroyOnDeath"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDestroyDelay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDeathSound"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDeathVolume"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDisableComponents"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shakeOnInstantDeath"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDeathShakeIntensity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instantDeathShakeDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onInstantDeath"));
                if (GUILayout.Button("Test: Instant Die") && Application.isPlaying)
                    script.InstantDie();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            showExplosionDeath = EditorGUILayout.Foldout(showExplosionDeath, "Explosion Death", true, EditorStyles.foldoutHeader);
            if (showExplosionDeath)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionDestroyOnDeath"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionDestroyDelay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionDeathSound"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionDeathVolume"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionForce"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionUpwardModifier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionAffectedBodies"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionDebrisDestroyDelay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionEffect"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("effectsOthersOnExplosion"));
                if (serializedObject.FindProperty("effectsOthersOnExplosion").boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("killRadius"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("killLayer"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionDisableComponents"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onExplosionDeath"));
                if (GUILayout.Button("Test: Die With Explosion") && Application.isPlaying)
                    script.DieWithExplosion();
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
