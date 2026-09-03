using System;
using Game.LevelOperations;
using UnityEngine;

namespace InfernoProtocol.BetterLights;

internal static class TorchColorService
{
    private static readonly Color NaturalFire = new(1f, 0.43f, 0.12f, 1f);
    private static readonly Color NaturalLantern = new(1f, 0.72f, 0.42f, 1f);

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

        return new LightTarget
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
    }

    internal static LightTarget CreateLanternTarget(Light light)
    {
        Transform root = FindLanternRoot(light);
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
            Key = BetterLightsPlugin.GetSceneLightKey("Lantern", root),
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

        Light[] lights = target.Root.GetComponentsInChildren<Light>(true);
        if (lights != null && lights.Length > 0 && lights[0] != null)
        {
            return lights[0].transform.position;
        }

        if (target.HasFlame)
        {
            ParticleSystem[] particles = target.Root.GetComponentsInChildren<ParticleSystem>(true);
            if (particles != null && particles.Length > 0 && particles[0] != null)
            {
                return particles[0].transform.position;
            }
        }

        return target.Root.position + (Vector3.up * 0.45f);
    }

    internal static Color ReadColor(LightTarget target)
    {
        if (target == null || !target.IsValid)
        {
            return target != null && !target.HasFlame ? NaturalLantern : NaturalFire;
        }

        Light[] lights = target.Root.GetComponentsInChildren<Light>(true);
        for (int i = 0; lights != null && i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                Color color = lights[i].color;
                color.a = 1f;
                return color;
            }
        }

        if (target.HasFlame)
        {
            ParticleSystem[] particles = target.Root.GetComponentsInChildren<ParticleSystem>(true);
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
        Light[] lights = target.Root.GetComponentsInChildren<Light>(true);
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
            changed += ApplyFlameColor(target.Root, color);
        }

        changed += ApplyEmissionColor(target.Root, color, target.HasFlame);
        return changed;
    }

    private static int ApplyFlameColor(Transform root, Color color)
    {
        int changed = 0;
        ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
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

        CompositeTorch[] compositeTorches = root.GetComponentsInChildren<CompositeTorch>(true);
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

    private static int ApplyEmissionColor(Transform root, Color color, bool hasFlame)
    {
        int changed = 0;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; renderers != null && i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsSmoke(renderer.gameObject.name))
            {
                continue;
            }

            try
            {
                Material material = renderer.material;
                if (material != null && ApplyMaterialColor(material, color, hasFlame))
                {
                    changed++;
                }
            }
            catch (Exception exception)
            {
                BetterLightsPlugin.ModLog.LogDebug($"Could not tint renderer {renderer.gameObject.name}: {exception.Message}");
            }
        }

        return changed;
    }

    private static bool ApplyMaterialColor(Material material, Color color, bool hasFlame)
    {
        bool changed = false;
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 2.4f);
            changed = true;
        }

        if (!hasFlame)
        {
            return changed;
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", color);
            changed = true;
        }

        string materialName = material.name ?? string.Empty;
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

    private static Transform FindLanternRoot(Light light)
    {
        if (light == null)
        {
            return null;
        }

        Transform current = light.transform;
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
