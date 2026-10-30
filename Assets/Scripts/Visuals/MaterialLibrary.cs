using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Hands out one shared material per colour.
    ///
    /// Materials are copied from the material Unity puts on a new primitive, so they use
    /// whatever shader the project's render pipeline expects. That keeps the scene from
    /// turning pink in URP or HDRP projects, which is what happens when a script asks for
    /// the Built-in pipeline's "Standard" shader by name.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();
        private static Material template;

        public static Material Get(Color color)
        {
            Color32 key = color;
            Material material;
            if (cache.TryGetValue(key, out material) && material != null) return material;

            material = new Material(Template());
            material.name = "Sim " + ColorUtility.ToHtmlStringRGB(color);
            // URP and HDRP call the main colour _BaseColor, the Built-in pipeline _Color.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            cache[key] = material;
            return material;
        }

        private static Material Template()
        {
            if (template != null) return template;

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "Material probe";
            Renderer probeRenderer = probe.GetComponent<Renderer>();
            template = probeRenderer != null ? probeRenderer.sharedMaterial : null;
            Object.Destroy(probe);

            if (template == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                template = new Material(shader);
            }
            return template;
        }
    }
}
