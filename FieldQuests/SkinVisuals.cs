using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerOperations;
using UnityEngine;

namespace InfernoProtocol.FieldQuests;

// Local cosmetics only: never edit shared asset materials or ItemData.
internal sealed class SkinVisuals : IDisposable
{
    private sealed class Applied
    {
        internal Renderer Renderer;
        internal Material[] Original, Clones;
        internal string Key;
        internal readonly List<Texture2D> Textures = new();
    }
    private readonly Dictionary<int, Applied> _applied = new();
    private readonly Dictionary<string, int> _choices = new();
    private float _next;
    internal static Color ColorFor(int finish) => finish switch
    {
        1 => new Color(.45f, .67f, .36f), 2 => new Color(.30f, .55f, .90f),
        3 => new Color(.88f, .35f, .18f), 4 => new Color(.76f, .57f, .28f),
        5 => new Color(.73f,.83f,.9f), 6 => new Color(.95f,.70f,.045f),
        7 => new Color(.8f,.44f,.15f), 8 => new Color(.8f,.12f,.19f),
        9 => new Color(.1f,.7f,.74f), 10 => new Color(.6f,.74f,.4f),
        11 => new Color(.65f,.35f,.8f), 12 => new Color(.82f,.68f,.45f), _ => Color.white
    };
    internal void Receive(QuestView view)
    {
        if (view.Skins == null) return;
        _choices.Clear();
        foreach (var pair in view.Skins)
            if (SkinCatalog.Items.Contains(pair.Key) && pair.Value > 0 && pair.Value < SkinCatalog.Names.Length)
                _choices[pair.Key] = pair.Value;
    }
    internal void Tick()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + .25f;
        var wanted = new HashSet<int>();
        Player p = Player.localPlayer;
        if (p != null)
        {
            Apply(p.staticItemRenderer?.meshRenderer, p.currentItemData.item.ToString(), wanted);
            foreach (PlayerClothing clothing in new[] { p.HatVisual, p.TopVisual, p.BottomVisual, p.ShoesVisual, p.GlovesVisual, p.AccessoryVisual, p.BackpackVisual })
                if (clothing != null) Apply(clothing.clothingRenderer, clothing.variantKey.ToString(), wanted);
        }
        foreach (int key in _applied.Keys.Where(k => !wanted.Contains(k)).ToArray()) Remove(key);
    }
    private void Apply(Renderer renderer, string item, HashSet<int> wanted)
    {
        if (renderer == null || !_choices.TryGetValue(item, out int finish)) return;
        int id = renderer.GetInstanceID(); wanted.Add(id);
        string key = item + ":" + finish;
        if (_applied.TryGetValue(id, out Applied existing))
        {
            var current = renderer.sharedMaterials;
            if (existing.Key == key && current.Length == existing.Clones.Length &&
                Enumerable.Range(0, current.Length).All(i => current[i] == existing.Clones[i])) return;
            Remove(id);
        }
        Material[] original = renderer.sharedMaterials;
        var clones = new Material[original.Length];
        var applied = new Applied { Renderer = renderer, Original = original, Clones = clones, Key = key };
        _applied[id] = applied;
        for (int i = 0; i < original.Length; i++)
        {
            if (original[i] == null) continue;
            clones[i] = new Material(original[i]);
            Texture2D texture = FinishTextures.Apply(clones[i], finish);
            if (texture != null) applied.Textures.Add(texture);
        }
        renderer.sharedMaterials = clones;
    }
    private void Remove(int id)
    {
        Applied a = _applied[id];
        if (a.Renderer != null)
        {
            Material[] current = a.Renderer.sharedMaterials;
            for (int i = 0; i < Math.Min(current.Length, a.Clones.Length); i++)
                if (current[i] == a.Clones[i]) current[i] = a.Original[i];
            a.Renderer.sharedMaterials = current;
        }
        foreach (Material material in a.Clones) if (material != null) UnityEngine.Object.Destroy(material);
        foreach (Texture2D texture in a.Textures) if (texture != null) UnityEngine.Object.Destroy(texture);
        _applied.Remove(id);
    }
    public void Dispose() { foreach (int id in _applied.Keys.ToArray()) Remove(id); _choices.Clear(); }
}
