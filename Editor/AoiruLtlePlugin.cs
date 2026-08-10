using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

[assembly: ExportsPlugin(typeof(Aoiru.ltle.AoiruLtlePlugin))]

namespace Aoiru.ltle
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    public sealed class AoiruLtlePlugin : Plugin<AoiruLtlePlugin>
    {
        public override string QualifiedName => "com.aoiru.ltle";
        public override string DisplayName => "lilToon Light Edit";

        private const string ShaderIdentifier = "liltoon";
        private const float FrameRate = 60f;

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .WithRequiredExtension(typeof(AnimatorServicesContext), seq =>
                {
                    seq.Run("ltle: Generate runtime layers", GenerateLtleRuntime);
                });
        }

        private void GenerateLtleRuntime(BuildContext ctx)
        {
            var avatarRoot = ctx.AvatarRootObject;

            var markers = avatarRoot.GetComponentsInChildren<AoiruLtleMarker>(true);

            if (markers.Length == 0) return;

            if (markers.Length > 1)
                Debug.LogWarning($"[ltle] Multiple AoiruLtleMarker found on '{avatarRoot.name}'. Using the first one.");

            var marker = markers[0];

            var slots = CollectLilToonSlots(avatarRoot);
            if (slots.Count == 0) return;

            var asc = ctx.Extension<AnimatorServicesContext>();
            var fx = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

            var propertyDefs = new[]
            {
                new PropertyDef { Name = "MinLightLimit", ShaderProp = "_LightMinLimit", RangeMax = 1f, MarkerValue = marker.minLightLimit },
                new PropertyDef { Name = "MaxLightLimit", ShaderProp = "_LightMaxLimit", RangeMax = 2f, MarkerValue = marker.maxLightLimit },
                new PropertyDef { Name = "MonochromeLighting", ShaderProp = "_MonochromeLighting", RangeMax = 1f, MarkerValue = marker.monochromeLighting },
                new PropertyDef { Name = "ShadowEnvStrength", ShaderProp = "_ShadowEnvStrength", RangeMax = 1f, MarkerValue = marker.shadowEnvStrength },
                new PropertyDef { Name = "AsUnlit", ShaderProp = "_AsUnlit", RangeMax = 1f, MarkerValue = marker.asUnlit },
            };

            var layerPriority = 100;
            foreach (var def in propertyDefs)
            {
                var clip = CreateCurveClip(def, slots);
                var paramName = $"Aoiru/ltle/{def.Name}";
                var normalizedDefault = Mathf.Clamp01(def.MarkerValue / def.RangeMax);

                fx.Parameters = fx.Parameters.SetItem(paramName, new AnimatorControllerParameter
                {
                    name = paramName,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = normalizedDefault,
                });

                var layer = VirtualLayer.Create(asc.ControllerContext.CloneContext, $"ltle_{def.Name}");
                layer.BlendingMode = AnimatorLayerBlendingMode.Override;
                layer.DefaultWeight = 1f;
                var state = layer.StateMachine.AddState(def.Name, clip);
                state.TimeParameter = paramName;
                state.WriteDefaultValues = true;

                fx.AddLayer(new LayerPriority(layerPriority), layer);
                layerPriority++;
            }

            AddParametersComponent(marker.gameObject, propertyDefs);
            AddMenuComponent(marker, propertyDefs, ctx);
        }

        private List<LilToonSlot> CollectLilToonSlots(GameObject root)
        {
            var slots = new List<LilToonSlot>();
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
            {
                var path = ComputeRelativePath(r.transform, root.transform);
                var materials = r.sharedMaterials;
                if (materials == null) continue;

                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null || mat.shader == null) continue;
                    if (!mat.shader.name.ToLowerInvariant().Contains(ShaderIdentifier)) continue;

                    slots.Add(new LilToonSlot
                    {
                        Path = path,
                        RendererType = r.GetType(),
                        SlotIndex = i,
                    });
                }
            }

            return slots;
        }

        private VirtualClip CreateCurveClip(PropertyDef def, List<LilToonSlot> slots)
        {
            var clip = VirtualClip.Create($"ltle_{def.Name}");
            clip.FrameRate = FrameRate;

            var curve = AnimationCurve.Linear(0f, 0f, 1f, def.RangeMax);

            foreach (var slot in slots)
            {
                var propName = slot.SlotIndex == 0
                    ? $"material.{def.ShaderProp}"
                    : $"material.{slot.SlotIndex}.{def.ShaderProp}";

                clip.SetFloatCurve(slot.Path, slot.RendererType, propName, curve);
            }

            return clip;
        }

        private void AddParametersComponent(GameObject target, PropertyDef[] defs)
        {
            var parameters = target.AddComponent<ModularAvatarParameters>();
            parameters.parameters = new List<ParameterConfig>();

            foreach (var def in defs)
            {
                parameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"Aoiru/ltle/{def.Name}",
                    syncType = ParameterSyncType.Float,
                    saved = true,
                    defaultValue = Mathf.Clamp01(def.MarkerValue / def.RangeMax),
                    hasExplicitDefaultValue = true,
                });
            }
        }

        private void AddMenuComponent(AoiruLtleMarker marker, PropertyDef[] defs, BuildContext ctx)
        {
            var icon = Resources.Load<Texture2D>("icon");

            // Inner menu: 5 RadialPuppet controls
            var innerMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            innerMenu.controls = new List<VRCExpressionsMenu.Control>();

            var labels = new[] { "Min Light Limit", "Max Light Limit", "Monochrome Lighting", "Shadow Env Strength", "As Unlit" };

            for (int i = 0; i < defs.Length; i++)
            {
                innerMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = labels[i],
                    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                    icon = icon,
                    subParameters = new[]
                    {
                        new VRCExpressionsMenu.Control.Parameter
                        {
                            name = $"Aoiru/ltle/{defs[i].Name}"
                        },
                    },
                });
            }

            ctx.AssetSaver.SaveAsset(innerMenu);

            // Wrapper menu: 1 SubMenu control pointing to inner menu
            var wrapperMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            wrapperMenu.controls = new List<VRCExpressionsMenu.Control>
            {
                new VRCExpressionsMenu.Control
                {
                    name = "lil Light",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = innerMenu,
                    icon = icon,
                },
            };

            ctx.AssetSaver.SaveAsset(wrapperMenu);

            var installer = marker.gameObject.AddComponent<ModularAvatarMenuInstaller>();
            installer.menuToAppend = wrapperMenu;
            if (marker.installTargetMenu != null)
                installer.installTargetMenu = marker.installTargetMenu;
        }

        private static string ComputeRelativePath(Transform child, Transform root)
        {
            var parts = new List<string>();
            var current = child;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        private struct PropertyDef
        {
            public string Name;
            public string ShaderProp;
            public float RangeMax;
            public float MarkerValue;
        }

        private struct LilToonSlot
        {
            public string Path;
            public System.Type RendererType;
            public int SlotIndex;
        }
    }
}
