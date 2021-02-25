using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelTable
{
    /// <summary>
    /// Gets or sets node Cell.
    /// </summary>
    public TableNodeCell[,] Cell { get; set; }

    /// <summary>
    /// Gets the mip level.
    /// </summary>
    public int MipLevel { get; }

    /// <summary>
    /// Gets or sets NodeCellCount.
    /// </summary>
    public int NodeCellCount { get; set; }

    /// <summary>
    /// Gets or sets PerCellSize.
    /// </summary>
    public int PerCellSize { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LevelTable"/> class.
    /// </summary>
    /// <param name="mip">mip.</param>
    /// <param name="tableSize">tablesize.</param>
    public LevelTable(int mip, int tableSize)
    {
        this.MipLevel = mip;
        this.PerCellSize = (int)Mathf.Pow(2, mip);
        this.NodeCellCount = tableSize / this.PerCellSize;
        this.Cell = new TableNodeCell[this.NodeCellCount, this.NodeCellCount];
        for (int i = 0; i < this.NodeCellCount; i++)
        {
            for (int j = 0; j < this.NodeCellCount; j++)
            {
                this.Cell[i, j] = new TableNodeCell(
                    i * this.PerCellSize,
                    j * this.PerCellSize,
                    this.PerCellSize,
                    this.PerCellSize,
                    this.MipLevel);
            }
        }
    }


    public class TableNodeCell
    {
        public RectInt Rect { get; set; }

        public int MipLevel { get; }

        public TableNodeCell(int x, int y, int width, int height, int mip)
        {
            this.Rect = new RectInt(x, y, width, height);
            this.MipLevel = mip;
        }
    }
}
