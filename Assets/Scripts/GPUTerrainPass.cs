using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GPUTerrainPass : ScriptableRenderPass
{
    public static Action<ScriptableRenderContext,Camera> ExecuteAction;
    public GPUTerrainPass()
    {
        this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }
    /// <inheritdoc/>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (HizBehaviour.Instance?.hizRT == null)
        {
            return;
        }

        ExecuteAction?.Invoke(context, renderingData.cameraData.camera);
    }
}
