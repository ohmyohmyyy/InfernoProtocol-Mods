using System;
using System.Collections.Generic;
using Game.LevelOperations;
using UnityEngine;

namespace InfernoProtocol.BetterLights;

internal static class TorchColorService
{
    private static readonly Color NaturalFire = new(1f, 0.43f, 0.12f, 1f);
    private static readonly Color NaturalLantern = new(1f, 0.72f, 0.42f, 1f);
    private static readonly string[] EmissionColorProperties =
    {
        "_EmissionColor",
        "_EmissiveColor",
        "_EmissiveColorLDR",
        "_EmissiveColorHDR",
        "_EmissionTint",
        "_EmissiveTint",
        "_GlowColor"
    };

    private static readonly string[] EmissionMapProperties =
    {
        "_EmissionMap",
        "_EmissiveColorMap",
        "_EmissiveMap",
        "_GlowMap"
    };

    internal static bool IsSupported(APlacable placable)
    {
        if (placable == null || !placable.gameObject.activeInHierarchy)
        {
            return false;
        }

        Items item = placable.placableItem;
        return item == Items.Torch || item == Items.StandingTorch || item == Items.WallTorch;
    }

    internal static LightTarget CreateTorchTarget(APlacable placable)
    {
        if (!IsSupported(placable))
        {
            return null;
        }

        LightTarget target = new()
        {
            Placable = placable,
            Root = placable.transform,
            Key = BetterLightsPlugin.GetTorchKey(placable),
            Name = placable.placableItem switch
            {
                Items.StandingTorch => "STANDING TORCH",
                Items.WallTorch => "WALL TORCH",
                _ => "TORCH"
            },
            HasFlame = true
        };
        target.Lights = target.Root.GetComponentsInChildren<Light>(true);
        if (target.Lights != null && target.Lights.Length > 0 && target.Lights[0] != null)
        {
            target.PrimaryLight = target.Lights[0];
            target.Visual = target.PrimaryLight.transform;
        }
        if (target.Visual == null)
        {
            target.Particles = target.Root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; target.Particles != null && i < target.Particles.Length; i++)
                if (target.Particles[i] != null && !IsSmoke(target.Particles[i].gameObject.name)) { target.Visual = target.Particles[i].transform; break; }
        }
        return target;
    }

    internal static LightTarget CreateLanternTarget(Light light)
    {
        Transform root = FindLanternRoot(light);
        Transform identity = FindLanternIdentity(light);
        if (root == null || !root.gameObject.activeInHierarchy)
        {
            return null;
        }

        APlacable placable = root.GetComponentInParent<APlacable>();
        if (IsSupported(placable))
        {
            return null;
        }

        return new LightTarget
        {
            Root = root,
            Visual = light.transform,
            PrimaryLight = light,
            // Keep the original named-light position as the persistent identity
            // even when Root expands upward to include the lantern mesh.
            Key = BetterLightsPlugin.GetSceneLightKey("Lantern", identity != null ? identity : root),
            Name = "LANTERN",
            HasFlame = false
        };
    }

    internal static Vector3 GetVisualPosition(LightTarget target)
    {
        if (target == null || !target.IsValid)
        {
            return Vector3.zero;
        }

        return target.Visual != null ? target.Visual.position : target.Root.position + (Vector3.up * 0.45f);
    }

    internal static Color ReadColor(LightTarget target)
    {
        if (target == null || !target.IsValid)
        {
            return target != null && !target.HasFlame ? NaturalLantern : NaturalFire;
        }

        Light primary = target.PrimaryLight;
        if (primary != null)
        {
            Color color = primary.color;
            color.a = 1f;
            return color;
        }

        Light[] lights = GetLights(target);
        for (int i = 0; lights != null && i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            target.PrimaryLight = lights[i];
            Color color = lights[i].color;
            color.a = 1f;
            return color;
        }

        if (target.HasFlame)
        {
            ParticleSystem[] particles = GetParticles(target);
            for (int i = 0; particles != null && i < particles.Length; i++)
            {
                if (particles[i] == null || IsSmoke(particles[i].gameObject.name))
                {
                    continue;
                }

                try
                {
                    Color color = particles[i].main.startColor.color;
                    color.a = 1f;
                    return color;
                }
                catch
                {
                    // Some gradient modes do not expose a representative color.
                }
            }
        }

        return target.HasFlame ? NaturalFire : NaturalLantern;
    }

    internal static int Apply(LightTarget target, Color color)
    {
        if (target == null || !target.IsValid)
        {
            return 0;
        }

        color.a = 1f;
        int changed = 0;
        Light[] lights = GetLights(target);
        for (int i = 0; lights != null && i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            light.color = color;
            changed++;
        }

        if (target.HasFlame)
        {
            changed += ApplyFlameColor(target, color);
        }

        changed += ApplyEmissionColor(target, color);
        return changed;
    }

    private static int ApplyFlameColor(LightTarget target, Color color)
    {
        int changed = 0;
        ParticleSystem[] particles = GetParticles(target);
        for (int i = 0; particles != null && i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null || IsSmoke(particle.gameObject.name))
            {
                continue;
            }

            try
            {
                ParticleSystem.MainModule main = particle.main;
                Color particleColor = color;
                float alpha = main.startColor.color.a;
                particleColor.a = alpha > 0.01f ? alpha : 1f;
                main.startColor = new ParticleSystem.MinMaxGradient(particleColor);
                changed++;
            }
            catch (Exception exception)
            {
                BetterLightsPlugin.ModLog.LogDebug($"Could not tint particle {particle.gameObject.name}: {exception.Message}");
            }
        }

        CompositeTorch[] compositeTorches = target.CompositeTorches ??= target.Root.GetComponentsInChildren<CompositeTorch>(true);
        for (int i = 0; compositeTorches != null && i < compositeTorches.Length; i++)
        {
            CompositeTorch composite = compositeTorches[i];
            if (composite == null)
            {
                continue;
            }

            composite.enabledEmessiveColor = color * 2.4f;
            changed++;
        }

        return changed;
    }

    private static int ApplyEmissionColor(LightTarget target, Color color)
    {
        int changed = 0;
        Material[] materials = GetEmissionMaterials(target);
        for (int i = 0; materials != null && i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            try
            {
                if (target.HasFlame)
                {
                    if (ApplyFlameMaterialColor(material, color))
                    {
                        changed++;
                    }
                }
                else if (ApplyLanternEmissionColor(material, color))
                {
                    changed++;
                }
            }
            catch (Exception exception)
            {
                BetterLightsPlugin.ModLog.LogDebug($"Could not tint emissive material: {exception.Message}");
            }
        }

        return changed;
    }

    private static Light[] GetLights(LightTarget target)
    {
        return target.Lights ??= target.Root.GetComponentsInChildren<Light>(true);
    }

    private static ParticleSystem[] GetParticles(LightTarget target)
    {
        return target.Particles ??= target.Root.GetComponentsInChildren<ParticleSystem>(true);
    }

    private static Material[] GetEmissionMaterials(LightTarget target)
    {
        if (target.EmissionMaterials != null) return target.EmissionMaterials;

        target.Renderers ??= target.Root.GetComponentsInChildren<Renderer>(true);
        List<Material> materials = new();
        for (int i = 0; target.Renderers != null && i < target.Renderers.Length; i++)
        {
            Renderer renderer = target.Renderers[i];
            if (renderer == null || IsSmoke(renderer.gameObject.name)) continue;

            if (target.HasFlame)
            {
                Material material = renderer.material;
                if (material != null) materials.Add(material);
                continue;
            }

            Material[] rendererMaterials = renderer.materials;
            for (int materialIndex = 0; rendererMaterials != null && materialIndex < rendererMaterials.Length; materialIndex++)
            {
                Material material = rendererMaterials[materialIndex];
                if (material != null) materials.Add(material);
            }
        }

        target.EmissionMaterials = materials.ToArray();
        return target.EmissionMaterials;
    }

    private static bool ApplyLanternEmissionColor(Material material, Color color)
    {
        bool changed = false;
        bool hasEmissionMap = HasEmissionMap(material);
        bool hasActiveEmission = false;
        float originalIntensity = 0f;
        for (int i = 0; i < EmissionColorProperties.Length; i++)
        {
            string property = EmissionColorProperties[i];
            if (!material.HasProperty(property))
            {
                continue;
            }

            Color existing = material.GetColor(property);
            float intensity = Mathf.Max(existing.r, Mathf.Max(existing.g, existing.b));
            originalIntensity = Mathf.Max(originalIntensity, intensity);
            hasActiveEmission |= intensity > 0.01f;
        }

        string materialName = material.name ?? string.Empty;
        bool looksLikeEmissiveSurface = ContainsAny(materialName, "emiss", "glow", "bulb", "glass");
        if (hasEmissionMap || hasActiveEmission || looksLikeEmissiveSurface)
        {
            // Preserve useful emission without turning a small mask into a large
            // HDR bloom source. Repeated color previews remain inside this cap.
            float hdrIntensity = Mathf.Clamp(originalIntensity, 0.8f, 2f);
            for (int i = 0; i < EmissionColorProperties.Length; i++)
            {
                string property = EmissionColorProperties[i];
                if (!material.HasProperty(property))
                {
                    continue;
                }

                bool ldrProperty = property.EndsWith("LDR", StringComparison.OrdinalIgnoreCase) ||
                                   property.IndexOf("Tint", StringComparison.OrdinalIgnoreCase) >= 0;
                material.SetColor(property, ldrProperty ? color : color * hdrIntensity);
                changed = true;
            }

            if (changed)
            {
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        return changed;
    }

    private static bool ApplyFlameMaterialColor(Material material, Color color)
    {
        bool changed = false;
        string materialName = material.name ?? string.Empty;
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 2.4f);
            changed = true;
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", color);
            changed = true;
        }

        bool looksLikeFlame = materialName.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              materialName.IndexOf("flame", StringComparison.OrdinalIgnoreCase) >= 0;
        if (looksLikeFlame && material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
            changed = true;
        }
        else if (looksLikeFlame && material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
            changed = true;
        }

        return changed;
    }

    private static bool HasEmissionMap(Material material)
    {
        for (int i = 0; i < EmissionMapProperties.Length; i++)
        {
            string property = EmissionMapProperties[i];
            if (material.HasProperty(property) && material.GetTexture(property) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            if (value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindLanternRoot(Light light)
    {
        if (light == null)
        {
            return null;
        }

        Transform current = light.transform;
        Transform namedLantern = null;
        int namedDepth = -1;
        for (int depth = 0; current != null && depth < 8; depth++)
        {
            if (ContainsLantern(current.gameObject.name))
            {
                namedLantern ??= current;
                if (namedDepth < 0)
                {
                    namedDepth = depth;
                }
            }

            if (namedLantern != null && depth - namedDepth <= 2)
            {
                Renderer[] renderers = current.GetComponentsInChildren<Renderer>(true);
                if (renderers != null && renderers.Length > 0 && renderers.Length <= 12)
                {
                    return current;
                }
            }
            else if (namedLantern != null)
            {
                break;
            }

            current = current.parent;
        }

        return namedLantern;
    }

    private static Transform FindLanternIdentity(Light light)
    {
        Transform current = light != null ? light.transform : null;
        for (int depth = 0; current != null && depth < 8; depth++)
        {
            if (ContainsLantern(current.gameObject.name))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool ContainsLantern(string value)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSmoke(string value)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
