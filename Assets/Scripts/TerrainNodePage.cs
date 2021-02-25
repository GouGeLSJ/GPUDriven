using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class TerrainNodePage
{
    public Rect rect;
    public int mip;
    public int index;
    public NodeInfo Info;
    public TerrainNodePage[] children;

    public TerrainNodePage(Rect r)
    {
        this.rect = r;
        this.index = -1;
        this.mip = -1;
    }

    public TerrainNodePage(Rect r, int m)
    {
        this.rect = r;
        this.mip = m;
        this.Info = new NodeInfo(new float4(r.xMin,r.yMin,r.width,r.height), m);
        this.index = -1;
        if (this.mip > 0)
        {
            children = new TerrainNodePage[4];
            children[0] = new TerrainNodePage(new Rect(r.xMin, r.yMin, r.width / 2, r.height / 2), m - 1);
            children[1] = new TerrainNodePage(new Rect(r.xMin + r.width / 2, r.yMin, r.width / 2, r.height / 2), m - 1);
            children[2] = new TerrainNodePage(new Rect(r.xMin + r.width / 2, r.yMin + r.height / 2, r.width / 2, r.height / 2), m - 1);
            children[3] = new TerrainNodePage(new Rect(r.xMin, r.yMin + r.height / 2, r.width / 2, r.height / 2), m - 1);
        }
    }

    public TerrainNodePage GetActiveNode(Vector2 center)
    {
        if (rect.Contains(center))
        {
            if (index >= 0)
            {
                return this;
            }
            else
            {
                foreach (var child in children)
                {
                    var ans = child.GetActiveNode(center);
                    if (ans != null)
                    {
                        return ans;
                    }
                }
            }
        }

        return null;
    }


    public void CollectNodeInfo(Vector2 center, List<NodeInfo> allNodeInfo)
    {
        if (mip >= 0 && (mip == 0 || (center - rect.center).magnitude >= 100 * Mathf.Pow(2, mip)))
        {
            this.index = allNodeInfo.Count;
            allNodeInfo.Add(this.Info);
        }
        else
        {
            this.index = -1;
            foreach (var child in children)
            {
                child.CollectNodeInfo(center, allNodeInfo);
            }
        }
    }
}

public struct NodeInfo
{
    public float4 rect;
    public int mip;
    public int neighbor;

    public NodeInfo(float4 r, int m)
    {
        rect = r;
        mip = m;
        neighbor = 0;
    }
}
