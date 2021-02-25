using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HizBehaviour : MonoBehaviour
{
    public RenderTexture hizRT = null;
    private Vector2Int lastSize = Vector2Int.zero;
    private Camera mainCam;
    const string m_ProfilerTag = "Hiz Pass";
    public static HizBehaviour Instance;

    private void OnEnable()
    {
        HizPass.HizRTFunc = getHizRT;
        Instance = this;
        mainCam = Camera.main;
    }
    private void OnDisable()
    {
        HizPass.HizRTFunc = null;
        Instance = null;
        this.Cleanup();
    }

    private void Cleanup()
    {
        if(hizRT != null)
        {
            DestroyImmediate(hizRT);
            hizRT = null;
        }
    }

    private RenderTexture getHizRT(ScriptableRenderContext context,Camera cam, Vector2Int size)
    {
        if(cam != mainCam)
        {
            return null;
        }
        if(hizRT != null && lastSize == size)
        {
            return hizRT;
        }
        this.Cleanup();

        var cmd = CommandBufferPool.Get(m_ProfilerTag);
        int width = Mathf.CeilToInt(size.x / 2f);
        int height = Mathf.CeilToInt(size.y / 2f);
        hizRT = new RenderTexture(width, height, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat, 7)
        {
            enableRandomWrite = true,
            useMipMap = true,
            autoGenerateMips = false,
        };
        cmd.SetGlobalTexture("_HiZMap", hizRT);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        lastSize = size;
        return hizRT;
    }

}
