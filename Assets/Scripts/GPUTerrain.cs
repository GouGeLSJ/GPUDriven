using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

public class GPUTerrain : MonoBehaviour
{
    public Terrain terrain;
    public Mesh instanceMesh;
    public Material mat;
    public ComputeShader cullingComputeShader;
    
    public int DebugMode = 1;

    private List<NodeInfo> allNodeInfo;
    private RenderTexture heightmapTex;
    private Texture2D normalTex;

    private ComputeBuffer allInstancesPosWSBuffer;
    private ComputeBuffer visibleInstancesOnlyPosWSIDBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer shadowBuffer;
    private int cullTerrainKernel;
    private int cullTerrainShadowKernel;
    private TerrainNodePage pageRoot;
    const string m_ProfilerTag = "Gpu Terrain";


    void OnEnable()
    {
        if(pageRoot == null)
        {
            float perSize = 64;
            var rect = new Rect(0, 0, terrain.terrainData.size.x, terrain.terrainData.size.z);
            pageRoot = new TerrainNodePage(rect);
            var children = new List<TerrainNodePage>();
            for (var i = rect.xMin; i < rect.xMax; i += perSize)
                for (var j = rect.yMin; j < rect.yMax; j += perSize)
                {
                    children.Add(new TerrainNodePage(new Rect(i, j, perSize, perSize), 3));
                }
            pageRoot.children = children.ToArray();
        }
        allNodeInfo = new List<NodeInfo>();
        var camPos = Camera.main.transform.position;
        var center = new Vector2(camPos.x, camPos.z);
        pageRoot.CollectNodeInfo(center, allNodeInfo);
        for(var i =0;i< allNodeInfo.Count;i++)
        {
            var nodeInfo = allNodeInfo[i];
            var nodeCenter = new Vector2(nodeInfo.rect.x + nodeInfo.rect.z * 0.5f, nodeInfo.rect.y + nodeInfo.rect.w * 0.5f);
            var topNode = pageRoot.GetActiveNode(nodeCenter + new Vector2(0, nodeInfo.rect.w));
            var bottomNode = pageRoot.GetActiveNode(nodeCenter + new Vector2(0, -nodeInfo.rect.w));
            var leftNode = pageRoot.GetActiveNode(nodeCenter + new Vector2(-nodeInfo.rect.z, 0));
            var rightNode = pageRoot.GetActiveNode(nodeCenter + new Vector2(nodeInfo.rect.z, 0));
            var nei = new bool4(topNode != null && topNode.mip > nodeInfo.mip,
                                          bottomNode != null && bottomNode.mip > nodeInfo.mip,
                                          leftNode != null && leftNode.mip > nodeInfo.mip,
                                          rightNode != null && rightNode.mip > nodeInfo.mip);
            nodeInfo.neighbor = (1 * (nei.x ? 1 : 0)) + ((1 << 1) * (nei.y ? 1 : 0)) + ((1 << 2) * (nei.z ? 1 : 0)) + ((1 << 3) * (nei.w ? 1 : 0));
            allNodeInfo[i] = nodeInfo;
        }

        heightmapTex = terrain.terrainData.heightmapTexture;
        normalTex = new Texture2D(heightmapTex.width, heightmapTex.height, TextureFormat.RGBA32, -1, true);
        var colors = new Color[heightmapTex.width * heightmapTex.width];
        int index = 0;
        for(int i=0;i<heightmapTex.width;i++)
            for(int j=0;j<heightmapTex.height;j++)
            {
                var normal = terrain.terrainData.GetInterpolatedNormal((float)i / heightmapTex.width, (float)j / heightmapTex.height);
                colors[index ++] = new Color( normal.z * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.x * 0.5f + 0.5f);
            }
        normalTex.SetPixels(colors);
        normalTex.Apply();

        allInstancesPosWSBuffer = new ComputeBuffer(allNodeInfo.Count, sizeof(float) * 4 + sizeof(int) + sizeof(int));
        allInstancesPosWSBuffer.SetData(allNodeInfo.ToArray());

        visibleInstancesOnlyPosWSIDBuffer = new ComputeBuffer(allNodeInfo.Count, sizeof(uint), ComputeBufferType.Append);

        if (argsBuffer != null)
            argsBuffer.Release();
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)instanceMesh.GetIndexCount(0);
        args[1] = (uint)allNodeInfo.Count;
        args[2] = (uint)instanceMesh.GetIndexStart(0);
        args[3] = (uint)instanceMesh.GetBaseVertex(0);
        args[4] = 0;

        argsBuffer.SetData(args);

        if (shadowBuffer != null)
            shadowBuffer.Release();
        shadowBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        shadowBuffer.SetData(args);

        cullTerrainKernel = this.cullingComputeShader.FindKernel("CullTerrain");
        cullTerrainShadowKernel = this.cullingComputeShader.FindKernel("CullTerrainShadow");
        cullingComputeShader.SetBuffer(cullTerrainKernel, "_AllInstancesPosWSBuffer", allInstancesPosWSBuffer);
        cullingComputeShader.SetBuffer(cullTerrainKernel, "_VisibleInstancesOnlyPosWSIDBuffer", visibleInstancesOnlyPosWSIDBuffer);
        cullingComputeShader.SetTexture(cullTerrainKernel, "_HeightMap", heightmapTex);

        cullingComputeShader.SetBuffer(cullTerrainShadowKernel, "_AllInstancesPosWSBuffer", allInstancesPosWSBuffer);
        cullingComputeShader.SetBuffer(cullTerrainShadowKernel, "_VisibleInstancesOnlyPosWSIDBuffer", visibleInstancesOnlyPosWSIDBuffer);
        cullingComputeShader.SetTexture(cullTerrainShadowKernel, "_HeightMap", heightmapTex);
        cullingComputeShader.SetFloat("_TerrainHeightSize", 2 * terrain.terrainData.size.y);
        mat.SetBuffer("_AllInstancesTransformBuffer", allInstancesPosWSBuffer);
        mat.SetBuffer("_VisibleInstanceOnlyTransformIDBuffer", visibleInstancesOnlyPosWSIDBuffer);

        Shader.SetGlobalTexture("_TerrainHeightmapTexture", heightmapTex);
        Shader.SetGlobalTexture("_TerrainNormalmapTexture", normalTex);
        Shader.SetGlobalVector("terrainParam", terrain.terrainData.size);

        debugtex = new RenderTexture(1024, 1024, 0, UnityEngine.Experimental.Rendering.DefaultFormat.LDR);
        debugtex.enableRandomWrite = true;

        ShadowUtils.CustomRenderShadowSlice += this.RenderShadowmap;
        GPUTerrainPass.ExecuteAction += Render;
    }

    RenderTexture debugtex;

    void OnDisable()
    {
        debugtex.Release();
        if (allInstancesPosWSBuffer != null)
            allInstancesPosWSBuffer.Release();
        allInstancesPosWSBuffer = null;

        if (visibleInstancesOnlyPosWSIDBuffer != null)
            visibleInstancesOnlyPosWSIDBuffer.Release();
        visibleInstancesOnlyPosWSIDBuffer = null;

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;

        if (shadowBuffer != null)
            shadowBuffer.Release();
        shadowBuffer = null;

        if (normalTex != null)
            DestroyImmediate(normalTex);
        normalTex = null;

        ShadowUtils.CustomRenderShadowSlice -= this.RenderShadowmap;
        GPUTerrainPass.ExecuteAction -= this.Render;
    }
    
    void Render(ScriptableRenderContext context, Camera cam)
    {
        var cmd = CommandBufferPool.Get(m_ProfilerTag);
        if (DebugMode < 0 || (DebugMode == 0 && cam == Camera.main))
        {
            var hizRT = HizBehaviour.Instance.hizRT;
            cmd.SetComputeTextureParam(cullingComputeShader, cullTerrainKernel, "_HiZMap", hizRT);
            cmd.SetComputeVectorParam(cullingComputeShader, "_HizSize", new Vector4(hizRT.width, hizRT.height, 0, 0));
            Matrix4x4 v = cam.worldToCameraMatrix;
            Matrix4x4 p = cam.projectionMatrix;
            Matrix4x4 vp = p * v;
            cmd.SetComputeBufferCounterValue(visibleInstancesOnlyPosWSIDBuffer, 0);
            cmd.SetComputeMatrixParam(cullingComputeShader, "_VPMatrix", vp);
            cmd.DispatchCompute(cullingComputeShader, cullTerrainKernel, Mathf.CeilToInt(allNodeInfo.Count / 64f), 1, 1);
            cmd.CopyCounterValue(visibleInstancesOnlyPosWSIDBuffer, argsBuffer, 4);
        }

        cmd.DrawMeshInstancedIndirect(instanceMesh, 0, mat, 0, argsBuffer);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void RenderShadowmap(CommandBuffer cmd, Matrix4x4 shadowTransform,Vector4 shadowBias, VisibleLight shadowLight,int cascadeIndex)
    {
        if (DebugMode < 0 || DebugMode == cascadeIndex + 1)
        {
            cmd.SetComputeBufferCounterValue(visibleInstancesOnlyPosWSIDBuffer, 0);
            cmd.SetComputeMatrixParam(cullingComputeShader, "_VPMatrix", shadowTransform);
            cmd.SetComputeVectorParam(cullingComputeShader, "_ShadowBias", shadowBias);
            Vector3 lightDirection = -shadowLight.localToWorldMatrix.GetColumn(2);
            cmd.SetComputeVectorParam(cullingComputeShader, "_LightDirection", lightDirection);
            cmd.DispatchCompute(cullingComputeShader, cullTerrainShadowKernel, Mathf.CeilToInt(allNodeInfo.Count / 64f), 1, 1);
            if (DebugMode == cascadeIndex + 1)
            {
                cmd.CopyCounterValue(visibleInstancesOnlyPosWSIDBuffer, argsBuffer, 4);
                return;
            }

            cmd.CopyCounterValue(visibleInstancesOnlyPosWSIDBuffer, shadowBuffer, 4);
            cmd.DrawMeshInstancedIndirect(instanceMesh, 0, mat, 1, shadowBuffer);
            
        }
    }
}
