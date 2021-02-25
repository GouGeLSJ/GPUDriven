using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HizFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Setting
    {
        public ComputeShader hizCS;
    }
    public Setting setting = new Setting();
    private HizPass hizPass;
    private GPUTerrainPass terrainPass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (hizPass != null && renderer is ForwardRenderer)
        {
            var forword = renderer as ForwardRenderer;
            hizPass.SetUp(setting, forword.DepthTexture);
            renderer.EnqueuePass(hizPass);
            renderer.EnqueuePass(terrainPass);
        }
    }

    public override void Create()
    {
        hizPass = new HizPass();
        terrainPass = new GPUTerrainPass();
    }
}