using Game.LevelOperations;
using UnityEngine;

namespace InfernoProtocol.BetterLights;

internal sealed class LightTarget
{
    internal APlacable Placable;
    internal Transform Root;
    internal Transform Visual;
    internal Light PrimaryLight;
    internal Light[] Lights;
    internal ParticleSystem[] Particles;
    internal CompositeTorch[] CompositeTorches;
    internal Renderer[] Renderers;
    internal Material[] EmissionMaterials;
    internal string Key;
    internal string Name;
    internal bool HasFlame;

    internal bool IsValid => Root != null && Root.gameObject.activeInHierarchy;
}
