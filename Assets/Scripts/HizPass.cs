using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HizPass : ScriptableRenderPass
{
    private ComputeShader computeShader;
    public static Func<ScriptableRenderContext,Camera, Vector2Int, RenderTexture>  HizRTFunc;
    private RenderTargetHandle depthTex;
    private Vector2Int depthSize;
    const string m_ProfilerTag = "Hiz Pass";
    private int HizTexTemp = Shader.PropertyToID("_HizTexTemp");

    public HizPass()
    {
        this.renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
    }
    public void SetUp(HizFeature.Setting setting, RenderTargetHandle depthTex)
    {
        this.computeShader = setting.hizCS;
        this.depthTex = depthTex;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        depthSize.x = cameraTextureDescriptor.width;
        depthSize.y = cameraTextureDescriptor.height;
    }

    /// <inheritdoc/>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (HizRTFunc != null)
        {
            var hizRT = HizRTFunc(context, renderingData.cameraData.camera, depthSize);
            if (hizRT != null)
            {
                var cmd = CommandBufferPool.Get(m_ProfilerTag);
                int width = hizRT.width;
                int height = hizRT.height;
                HizTexTemp = Shader.PropertyToID("_HizTexTemp");        //这里同一张贴图不同等级mipmap不能既当输入又当输出，采用pingpang方式写入mipmap
                for (int i = 0; i < hizRT.mipmapCount; i++)
                {
                    if (i % 2 == 1)
                    {
                        if (i > 1)
                        {
                            cmd.ReleaseTemporaryRT(HizTexTemp);
                        }
                        cmd.GetTemporaryRT(HizTexTemp, width, height, 0, hizRT.filterMode, hizRT.format, RenderTextureReadWrite.Linear, hizRT.antiAliasing, true);
                        cmd.SetComputeTextureParam(computeShader, 0, "DepthTex", hizRT, i - 1);  // input mipmap not work
                        cmd.SetComputeFloatParam(computeShader, "uvScale", Mathf.Pow(2,i));
                        cmd.SetComputeTextureParam(computeShader, 0, "HizTex", HizTexTemp);
                    }
                    else
                    {
                        if (i == 0)
                            cmd.SetComputeTextureParam(computeShader, 0, "DepthTex", this.depthTex.Identifier());
                        else
                            cmd.SetComputeTextureParam(computeShader, 0, "DepthTex", HizTexTemp);
                        cmd.SetComputeFloatParam(computeShader, "uvScale", 2);
                        cmd.SetComputeTextureParam(computeShader, 0, "HizTex", hizRT, i);
                    }
                    cmd.DispatchCompute(computeShader, 0, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                    width /= 2;
                    height /= 2;

                    if (i % 2 == 1)
                        cmd.CopyTexture(HizTexTemp,0,0, hizRT, 0,i);
                }

                cmd.ReleaseTemporaryRT(HizTexTemp);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
