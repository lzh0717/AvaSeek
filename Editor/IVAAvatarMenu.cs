using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using IVAAvatar;

namespace IVAAvatar.EditorTools
{
    /// <summary>
    /// One-click scene setup. Everything is a normal, undoable scene edit - nothing is
    /// written to disk and no asset is imported.
    /// </summary>
    public static class IVAAvatarMenu
    {
        const string AvatarName = "IVA_Avatar";

        [MenuItem("GameObject/IVA Avatar/Avatar", false, 10)]
        public static void CreateAvatar()
        {
            IVARenderer existing = Object.FindFirstObjectByType<IVARenderer>();
            if (existing != null)
            {
                Select(existing.gameObject);
                Debug.Log("[IVA] An avatar already exists in this scene - selected it.");
                return;
            }

            var go = new GameObject(AvatarName);
            Undo.RegisterCreatedObjectUndo(go, "Create IVA Avatar");

            // Parent to the current selection when there is one, so the avatar can be
            // dropped straight onto a dashboard / panel / rig.
            if (Selection.activeTransform != null) go.transform.SetParent(Selection.activeTransform, false);

            Undo.AddComponent<IVARenderer>(go);
            Select(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[IVA] Created '" + AvatarName + "' with IVARenderer.");
        }

        [MenuItem("GameObject/IVA Avatar/Demo (camera + avatar + sliders)", false, 11)]
        public static void CreateDemo()
        {
            // Avatar
            IVARenderer avatar = Object.FindFirstObjectByType<IVARenderer>();
            if (avatar == null)
            {
                var go = new GameObject(AvatarName);
                Undo.RegisterCreatedObjectUndo(go, "Create IVA Demo");
                avatar = Undo.AddComponent<IVARenderer>(go);
            }

            // Camera - reuse the scene's main camera if there is one.
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                Undo.RegisterCreatedObjectUndo(camGO, "Create IVA Demo");
                camGO.tag = "MainCamera";
                cam = camGO.GetComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f);

            IVACameraSetup framing = cam.GetComponent<IVACameraSetup>();
            if (framing == null) framing = Undo.AddComponent<IVACameraSetup>(cam.gameObject);
            framing.target = avatar.transform;
            framing.distance = 5f;
            framing.orthographicSize = 1f;

            // Slider panel
            if (Object.FindFirstObjectByType<IVAParameterPanel>() == null)
            {
                var panelGO = new GameObject("IVA Parameter Panel");
                Undo.RegisterCreatedObjectUndo(panelGO, "Create IVA Demo");
                IVAParameterPanel panel = Undo.AddComponent<IVAParameterPanel>(panelGO);
                panel.avatar = avatar;
            }

            Select(avatar.gameObject);
            EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
            Debug.Log("[IVA] Demo scene ready - press Play. The sliders build themselves from " +
                      IVAParameters.Names.Count + " parameters.");
        }

        /// <summary>Prints every parameter and its range - handy when wiring an external driver.</summary>
        [MenuItem("Tools/IVA Avatar/Log Parameters")]
        public static void LogParameters()
        {
            IVARenderer avatar = Object.FindFirstObjectByType<IVARenderer>();
            var names = IVAParameters.Names;
            Debug.Log("[IVA] " + names.Count + " parameter(s):");
            for (int i = 0; i < names.Count; i++)
            {
                IVAParameters.TryGetRange(names[i], out float min, out float max);
                string current = "";
                if (avatar != null && IVAParameters.TryGet(avatar, names[i], out float v))
                    current = "  = " + v.ToString("F3");
                Debug.Log("  " + names[i] + "  [" + min + ", " + max + "]" + current);
            }
        }

        static void Select(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}
