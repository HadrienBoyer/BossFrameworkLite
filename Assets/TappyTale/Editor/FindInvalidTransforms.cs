#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TappyTale.EditorTools
{
    public static class FindInvalidTransforms
    {
        [MenuItem("Tools/Tappy Tale/Diagnostics/Find Invalid AABB")]
        private static void FindInvalidAabb()
        {
            int issues = 0;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Transform[] transforms =
                        root.GetComponentsInChildren<Transform>(true);

                    foreach (Transform transform in transforms)
                    {
                        issues += CheckTransform(transform);
                    }

                    Renderer[] renderers =
                        root.GetComponentsInChildren<Renderer>(true);

                    foreach (Renderer renderer in renderers)
                    {
                        issues += CheckRenderer(renderer);
                    }
                }
            }

            if (issues == 0)
            {
                Debug.Log(
                    "[Tappy Tale] No invalid Transform or Renderer bounds found."
                );

                return;
            }

            Debug.LogError(
                $"[Tappy Tale] Found {issues} invalid value(s). " +
                "Check the errors above."
            );
        }

        private static int CheckTransform(Transform transform)
        {
            int issues = 0;

            if (!IsFinite(transform.position))
            {
                LogIssue(
                    transform.gameObject,
                    $"Invalid world position: {transform.position}"
                );

                issues++;
            }

            if (!IsFinite(transform.localPosition))
            {
                LogIssue(
                    transform.gameObject,
                    $"Invalid local position: {transform.localPosition}"
                );

                issues++;
            }

            if (!IsFinite(transform.localScale))
            {
                LogIssue(
                    transform.gameObject,
                    $"Invalid local scale: {transform.localScale}"
                );

                issues++;
            }

            Quaternion rotation = transform.rotation;

            if (!IsFinite(rotation))
            {
                LogIssue(
                    transform.gameObject,
                    $"Invalid rotation: {rotation}"
                );

                issues++;
            }

            return issues;
        }

        private static int CheckRenderer(Renderer renderer)
        {
            Bounds bounds = renderer.bounds;

            if (IsFinite(bounds.center) &&
                IsFinite(bounds.extents) &&
                IsFinite(bounds.size))
            {
                return 0;
            }

            LogIssue(
                renderer.gameObject,
                $"Invalid Renderer bounds. " +
                $"Center={bounds.center}, " +
                $"Extents={bounds.extents}, " +
                $"Size={bounds.size}, " +
                $"Renderer={renderer.GetType().Name}"
            );

            return 1;
        }

        private static bool IsFinite(Vector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z) &&
                IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                    !float.IsInfinity(value);
        }

        private static void LogIssue(
            GameObject gameObject,
            string message
        )
        {
            Debug.LogError(
                $"[Tappy Tale] INVALID AABB SOURCE\n" +
                $"{GetHierarchyPath(gameObject.transform)}\n" +
                $"{message}",
                gameObject
            );
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;

            Transform parent = transform.parent;

            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }
    }
}

#endif
