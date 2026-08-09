using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Aoiru.ltle
{
    public class AoiruLtleWindow : EditorWindow
    {
        private VRCAvatarDescriptor avatar;
        private VRCExpressionsMenu installTargetMenu;
        private float minLightLimit;
        private float maxLightLimit = 1f;
        private float monochromeLighting;
        private float shadowEnvStrength;
        private float asUnlit;
        private bool autoApply;

        private const string ShaderIdentifier = "liltoon";

        [MenuItem("Tools/Aoiru/lilToon Light Edit")]
        private static void Open()
        {
            var window = GetWindow<AoiruLtleWindow>("lilToon Light Edit");
            window.minSize = new Vector2(360, 290);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", avatar, typeof(VRCAvatarDescriptor), true);
            if (EditorGUI.EndChangeCheck())
            {
                // No auto-snap of slider values. The user sets them manually.
            }

            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            minLightLimit = EditorGUILayout.Slider("Min Light Limit", minLightLimit, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && autoApply) Apply();

            EditorGUI.BeginChangeCheck();
            maxLightLimit = EditorGUILayout.Slider("Max Light Limit", maxLightLimit, 0f, 2f);
            if (EditorGUI.EndChangeCheck() && autoApply) Apply();

            EditorGUI.BeginChangeCheck();
            monochromeLighting = EditorGUILayout.Slider("Monochrome Lighting", monochromeLighting, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && autoApply) Apply();

            EditorGUI.BeginChangeCheck();
            shadowEnvStrength = EditorGUILayout.Slider("Shadow Env Strength", shadowEnvStrength, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && autoApply) Apply();

            EditorGUI.BeginChangeCheck();
            asUnlit = EditorGUILayout.Slider("As Unlit", asUnlit, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && autoApply) Apply();

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply"))
            {
                Apply();
            }
            autoApply = EditorGUILayout.Toggle("Auto Apply", autoApply);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            installTargetMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField("Target Menu", installTargetMenu, typeof(VRCExpressionsMenu), false);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Install Runtime Tool"))
            {
                Install();
            }
        }

        private void Apply()
        {
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("ltle", "No avatar assigned.", "OK");
                return;
            }

            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            Undo.SetCurrentGroupName("ltle Apply");
            var undoGroup = Undo.GetCurrentGroup();

            foreach (var r in renderers)
            {
                var materials = r.sharedMaterials;
                if (materials == null) continue;

                foreach (var mat in materials)
                {
                    if (mat == null) continue;
                    if (!IsLilToonMaterial(mat)) continue;

                    Undo.RecordObject(mat, "ltle Apply");
                    mat.SetFloat("_LightMinLimit", minLightLimit);
                    mat.SetFloat("_LightMaxLimit", maxLightLimit);
                    mat.SetFloat("_MonochromeLighting", monochromeLighting);
                    mat.SetFloat("_ShadowEnvStrength", shadowEnvStrength);
                    mat.SetFloat("_AsUnlit", asUnlit);
                }
            }

            var markers = avatar.GetComponentsInChildren<AoiruLtleMarker>(true);
            if (markers.Length > 0)
            {
                var marker = markers[0];
                Undo.RecordObject(marker, "ltle Apply");
                marker.minLightLimit = minLightLimit;
                marker.maxLightLimit = maxLightLimit;
                marker.monochromeLighting = monochromeLighting;
                marker.shadowEnvStrength = shadowEnvStrength;
                marker.asUnlit = asUnlit;
                marker.installTargetMenu = installTargetMenu;
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private void Install()
        {
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("ltle", "No avatar assigned.", "OK");
                return;
            }

            var existing = avatar.GetComponentsInChildren<AoiruLtleMarker>(true);
            if (existing.Length > 0)
            {
                var path = GetTransformPath(existing[0].transform);
                EditorUtility.DisplayDialog("ltle", $"Already installed on:\n{path}", "OK");
                return;
            }

            var go = new GameObject("lilToon Light Edit");
            go.transform.SetParent(avatar.transform);
            Undo.RegisterCreatedObjectUndo(go, "ltle Install");

            var marker = go.AddComponent<AoiruLtleMarker>();
            marker.minLightLimit = minLightLimit;
            marker.maxLightLimit = maxLightLimit;
            marker.monochromeLighting = monochromeLighting;
            marker.shadowEnvStrength = shadowEnvStrength;
            marker.asUnlit = asUnlit;
            marker.installTargetMenu = installTargetMenu;

            EditorUtility.DisplayDialog("ltle", "Installed successfully.", "OK");
        }

        private static bool IsLilToonMaterial(Material mat)
        {
            return mat.shader != null
                && mat.shader.name.ToLowerInvariant().Contains(ShaderIdentifier);
        }

        private static string GetTransformPath(Transform t)
        {
            var path = t.name;
            var parent = t.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
