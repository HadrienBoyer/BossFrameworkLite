#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
//using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace TappyTale.EditorTools.GameStudio
{
    internal sealed class EmbeddedEditorWindowHost : IDisposable
    {
        private Type m_Type;
        private EditorWindow m_Window;
        private MethodInfo m_OnGUI;
        // private PropertyTree m_OdinTree;
        private string m_LastError;

        public Type HostedType => m_Type;
        // public bool IsValid =>
        //     m_Window != null &&
        //     (m_OnGUI != null || m_OdinTree != null) &&
        //     string.IsNullOrEmpty(m_LastError);

        public string Error => m_LastError;

        public bool TryHost(Type type)
        {
            if (type == null ||
                !typeof(EditorWindow).IsAssignableFrom(type))
            {
                m_LastError =
                    "The selected type is not an EditorWindow.";
                return false;
            }

            // if (m_Type == type &&
            //     m_Window != null &&
            //     (m_OnGUI != null || m_OdinTree != null))
            // {
            //     return true;
            // }

            DisposeHostedWindow();

            m_Type = type;

            try
            {
                m_Window =
                    ScriptableObject.CreateInstance(type)
                    as EditorWindow;

                if (m_Window == null)
                {
                    m_LastError =
                        $"Unable to create {type.FullName}.";
                    return false;
                }

                m_Window.hideFlags =
                    HideFlags.HideAndDontSave;

                // if (m_Window is OdinEditorWindow)
                // {
                //     // Draw Odin's inspector directly in the host's layout and scroll view.
                //     m_OdinTree = PropertyTree.Create(m_Window);
                //     m_LastError = null;
                //     return true;
                // }

                m_OnGUI =
                    FindInstanceMethod(
                        type,
                        "OnGUI");

                if (m_OnGUI == null)
                {
                    m_LastError =
                        $"{type.Name} has no OnGUI method to embed.";
                    DisposeHostedWindow();
                    return false;
                }

                m_LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                m_LastError =
                    Unwrap(exception).Message;
                DisposeHostedWindow();
                return false;
            }
        }

        public void Draw()
        {
            if (m_Window == null //||
                                 //(m_OnGUI == null && m_OdinTree == null)
                )
            {
                return;
            }

            try
            {
                // if (m_OdinTree != null)
                // {
                //     m_OdinTree.Draw(false);
                //     return;
                // }
                m_OnGUI.Invoke(
                    m_Window,
                    Array.Empty<object>());
            }
            catch (Exception exception)
            {
                Exception root =
                    Unwrap(exception);

                m_LastError =
                    root.GetType().Name +
                    ": " +
                    root.Message;

                EditorGUILayout.HelpBox(
                    "This legacy window could not be embedded safely.\n\n" +
                    m_LastError,
                    MessageType.Warning);
            }
        }

        public void Dispose()
        {
            DisposeHostedWindow();
        }

        private void DisposeHostedWindow()
        {
            // m_OdinTree?.Dispose();
            // m_OdinTree = null;
            if (m_Window != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    m_Window);
            }

            m_Window = null;
            m_OnGUI = null;
            m_Type = null;
        }

        private static MethodInfo FindInstanceMethod(
            Type type,
            string methodName)
        {
            Type cursor = type;

            while (cursor != null)
            {
                MethodInfo method =
                    cursor.GetMethod(
                        methodName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                if (method != null)
                {
                    return method;
                }

                cursor = cursor.BaseType;
            }

            return null;
        }

        private static Exception Unwrap(
            Exception exception)
        {
            while (exception is
                   TargetInvocationException invocation &&
                   invocation.InnerException != null)
            {
                exception =
                    invocation.InnerException;
            }

            return exception;
        }
    }
}
#endif
