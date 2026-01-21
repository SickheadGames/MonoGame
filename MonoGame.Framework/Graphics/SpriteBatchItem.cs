// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace Microsoft.Xna.Framework.Graphics
{
    internal struct SpriteSortItem
    {
        public int Index;
        public float SortKey;
    }

    internal class SpriteBatchItem
	{
		public Texture2D Texture;

        public Vector2 vertexTL;
        public Vector2 vertexTR;
        public Vector2 vertexBL;
        public Vector2 vertexBR;

        public Vector2 texCoordTL;
        public Vector2 texCoordBR;
        public Color color;
        public float depth;

        public SpriteBatchItem ()
		{        
		}
		
		public void Set ( float x, float y, float dx, float dy, float w, float h, float sin, float cos, Color color, Vector2 texCoordTL, Vector2 texCoordBR, float depth )
		{
            this.color = color;
            this.texCoordTL = texCoordTL;
            this.texCoordBR = texCoordBR;
            this.depth = depth;

            // TODO, Should we be just assigning the Depth Value to Z?
            // According to http://blogs.msdn.com/b/shawnhar/archive/2011/01/12/spritebatch-billboards-in-a-3d-world.aspx
            // We do.
			vertexTL.X = x+dx*cos-dy*sin;
            vertexTL.Y = y+dx*sin+dy*cos;

			vertexTR.X = x+(dx+w)*cos-dy*sin;
            vertexTR.Y = y+(dx+w)*sin+dy*cos;

			vertexBL.X = x+dx*cos-(dy+h)*sin;
            vertexBL.Y = y+dx*sin+(dy+h)*cos;

			vertexBR.X = x+(dx+w)*cos-(dy+h)*sin;
            vertexBR.Y = y+(dx+w)*sin+(dy+h)*cos;
		}

        public void Set(float x, float y, float w, float h, Color color, Vector2 texCoordTL, Vector2 texCoordBR, float depth)
        {
            this.color = color;
            this.texCoordTL = texCoordTL;
            this.texCoordBR = texCoordBR;
            this.depth = depth;

            vertexTL.X = x;
            vertexTL.Y = y;

            vertexTR.X = x + w;
            vertexTR.Y = y;

            vertexBL.X = x;
            vertexBL.Y = y + h;

            vertexBR.X = x + w;
            vertexBR.Y = y + h;
        }
    }
}

