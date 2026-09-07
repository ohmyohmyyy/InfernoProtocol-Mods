using System;
using UnityEngine;

namespace InfernoProtocol.FieldQuests;

internal static class FinishTextures
{
    internal static Texture2D Create(int finish, Texture source = null)
    {
        const int size = 512;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, true);
        RenderTexture temporary = null, previous = RenderTexture.active;
        try
        {
            Color32[] original = null;
            if (source != null)
            {
                temporary = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                original = texture.GetPixels32();
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                FinishPattern.Ink ink = FinishPattern.Sample(finish, x / (float)size, y / (float)size);
                Color32 old = original == null ? new Color32(255,255,255,255) : original[index];
                // Keep baked shading and texture detail; retain the original alpha mask exactly.
                float detail = original == null ? 1 : .35f + .65f * (old.r * .2126f + old.g * .7152f + old.b * .0722f) / 255;
                pixels[index] = new Color32((byte)(ink.R * detail * 255), (byte)(ink.G * detail * 255), (byte)(ink.B * detail * 255), old.a);
            }
            texture.SetPixels32(pixels); texture.Apply(true, true);
            texture.name = "FieldQuests_Finish_" + finish;
            texture.wrapMode = source != null ? source.wrapMode : TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            return texture;
        }
        catch { UnityEngine.Object.Destroy(texture); throw; }
        finally
        {
            RenderTexture.active = previous;
            if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
        }
    }
    internal static Texture2D Apply(Material material, int finish)
    {
        string map = material.HasProperty("_BaseMap") ? "_BaseMap" : material.HasProperty("_BaseColorMap") ? "_BaseColorMap" :
            material.HasProperty("_MainTex") ? "_MainTex" : null;
        if (map == null) return null;
        Texture2D texture = Create(finish, material.GetTexture(map));
        material.SetTexture(map, texture);
        // Do not touch normal maps, metallic masks, emission or alpha-test settings.
        return texture;
    }
}
