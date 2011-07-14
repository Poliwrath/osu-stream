using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using osum.Helpers;
using osum.Graphics.Sprites;
using Color = OpenTK.Graphics.Color4;
#if iOS
using OpenTK.Graphics.ES11;
using MonoTouch.Foundation;
using MonoTouch.ObjCRuntime;
using MonoTouch.OpenGLES;

using TextureTarget = OpenTK.Graphics.ES11.All;
using TextureParameterName = OpenTK.Graphics.ES11.All;
using EnableCap = OpenTK.Graphics.ES11.All;
using ArrayCap = OpenTK.Graphics.ES11.All;
using BlendingFactorSrc = OpenTK.Graphics.ES11.All;
using BlendingFactorDest = OpenTK.Graphics.ES11.All;
using PixelStoreParameter = OpenTK.Graphics.ES11.All;
using VertexPointerType = OpenTK.Graphics.ES11.All;
using ColorPointerType = OpenTK.Graphics.ES11.All;
using DepthFunction = OpenTK.Graphics.ES11.All;
using ClearBufferMask = OpenTK.Graphics.ES11.All;
using TexCoordPointerType = OpenTK.Graphics.ES11.All;
using BeginMode = OpenTK.Graphics.ES11.All;
using MatrixMode = OpenTK.Graphics.ES11.All;
using PixelInternalFormat = OpenTK.Graphics.ES11.All;
using PixelFormat = OpenTK.Graphics.ES11.All;
using PixelType = OpenTK.Graphics.ES11.All;
using ShaderType = OpenTK.Graphics.ES11.All;
using VertexAttribPointerType = OpenTK.Graphics.ES11.All;
using ProgramParameter = OpenTK.Graphics.ES11.All;
using ShaderParameter = OpenTK.Graphics.ES11.All;
using ErrorCode = OpenTK.Graphics.ES11.All;
using TextureEnvParameter = OpenTK.Graphics.ES11.All;
using TextureEnvTarget =  OpenTK.Graphics.ES11.All;
#else
using OpenTK.Input;
using OpenTK.Graphics.OpenGL;
#endif

using osum.Graphics;
using osum;
using System.Collections.Generic;
using osum.GameplayElements;
using osum.Graphics.Skins;
using osum.Graphics.Primitives;
using System.Drawing;


namespace osum.Graphics.Renderers
{
    /// <summary>
    /// Class to handle drawing of Greg's enhanced sliders.
    /// </summary>
    internal class SliderTrackRenderer : IDisposable
    {
        protected const int MAXRES = 24; // A higher MAXRES produces rounder endcaps at the cost of more vertices

        // Make the quad overhang just slightly to avoid 1px holes between a quad and a wedge from rounding errors.
        protected const float QUAD_OVERLAP_FUDGE = 6.0e-4f;

        // If the peak vertex of a quad is at exactly 0, we get a crack running down the center of horizontal linear sliders.
        // We shift the vertex slightly off to the side to avoid this.
        protected const float QUAD_MIDDLECRACK_FUDGE = 1.0e-4f;

        // Bias to the number of polygons to render in a given wedge. Also ... fixes ... holes.
        protected const float WEDGE_COUNT_FUDGE = 0.2f; // Seems this fudge is needed for osu!m

        // how much to trim off the inside of the texture
        protected const float TEXTURE_SHRINKAGE_FACTOR = 0.0f;

        // how far towards the inside do we slide the texture
#if iOS
        protected const float TEXEL_ORIGIN = 0.25f;
#else
        protected const float TEXEL_ORIGIN = 0.5f;
#endif
        protected const int COLOUR_COUNT = 4;

        protected int numIndices_quad;
        protected int numIndices_cap;
        protected int numPrimitives_quad;
        protected int numPrimitives_cap;
        protected int numVertices_quad;
        protected int numVertices_cap;

        protected float[][] coordinates_cap;
        protected float[] vertices_cap;

        GCHandle[] coordinates_cap_handle;
        IntPtr[] coordinates_cap_pointer;

        GCHandle vertices_cap_handle;
        IntPtr vertices_cap_pointer;

        protected float[][] coordinates_quad;
        protected float[] vertices_quad;

        GCHandle[] coordinates_quad_handle;
        IntPtr[] coordinates_quad_pointer;

        GCHandle vertices_quad_handle;
        IntPtr vertices_quad_pointer;

        // initialization
        protected bool am_initted_geom = false;
        bool boundEvents;

        // size of the sprite sheet if this were a retina screen
        private int retinaHeight;

        // ptexture info of where in the sheet our tracks are
        private int sheetY, sheetHeight;

        // cache actual texel coordinates for x-axis since they are always the same
        private float sheetStart, sheetEnd;

        /// <summary>
        /// Performs all advanced computation needed to draw sliders in a particular beatmap.
        /// </summary>
        internal void Init()
        {
            numVertices_quad = 6;
            numPrimitives_quad = 4;
            numIndices_quad = 6;

            numVertices_cap = MAXRES + 2;
            numPrimitives_cap = MAXRES;
            numIndices_cap = 3 * MAXRES;

            pTexture texture = TextureManager.Load(OsuTexture.tracks);

            float retinaWidth = texture.TextureGl.TextureWidth;
            retinaHeight = texture.TextureGl.TextureHeight;
            float sheetX = texture.X;
            sheetY = texture.Y;
            sheetHeight = texture.Height;

            sheetStart = (1.0f + sheetX + TEXEL_ORIGIN) / retinaWidth;
            sheetEnd = (-1.0f + sheetX + texture.Width + TEXEL_ORIGIN - TEXTURE_SHRINKAGE_FACTOR) / retinaWidth;

            CalculateCapMesh();
            CalculateQuadMesh();

            am_initted_geom = true;
        }

        /// <summary>
        /// The cap mesh is a half cone.
        /// </summary>
        private void CalculateCapMesh()
        {
            vertices_cap = new float[(numVertices_cap) * 3];
            coordinates_cap = new float[COLOUR_COUNT][];
            coordinates_cap_handle = new GCHandle[COLOUR_COUNT];
            coordinates_cap_pointer = new IntPtr[COLOUR_COUNT];

            float maxRes = (float)MAXRES;
            float step = MathHelper.Pi / maxRes;

            // the commented out lines are already set 0 from initialization.
            // they are kept for completeness.
            //vertices_cap[0] = 0.0f;
            //vertices_cap[1] = 0.0f;
            vertices_cap[2] = 1.0f;

            //vertices_cap[3] = 0.0f;
            vertices_cap[4] = -1.0f;
            //vertices_cap[5] = 0.0f;

            for (int z = 1; z < MAXRES; z++)
            {
                float angle = (float)z * step;
                vertices_cap[z * 3 + 3] = (float)(Math.Sin(angle));
                vertices_cap[z * 3 + 4] = -(float)(Math.Cos(angle));
                //vertices_cap[z * 3 + 5] = 0.0f;
            }

            //vertices_cap[MAXRES * 3 + 3] = 0.0f;
            vertices_cap[MAXRES * 3 + 4] = 1.0f;
            //vertices_cap[MAXRES * 3 + 5] = 0.0f;

            for (int x = 0; x < COLOUR_COUNT; x++)
            {
                float y = (2.0f + 4.0f * x + sheetY + TEXEL_ORIGIN) / retinaHeight;

                float[] this_coordinates = new float[(numVertices_cap) * 2];
                this_coordinates[0] = sheetEnd;
                this_coordinates[1] = y;

                this_coordinates[2] = sheetStart;
                this_coordinates[3] = y;

                for (int z = 1; z < MAXRES; z++)
                {
                    this_coordinates[z * 2 + 2] = sheetStart;
                    this_coordinates[z * 2 + 3] = y;
                }

                this_coordinates[MAXRES * 2 + 2] = sheetStart;
                this_coordinates[MAXRES * 2 + 3] = y;

                coordinates_cap[x] = this_coordinates;

                coordinates_cap_handle[x] = GCHandle.Alloc(coordinates_cap[x], GCHandleType.Pinned);
                coordinates_cap_pointer[x] = coordinates_cap_handle[x].AddrOfPinnedObject();
            }

            vertices_cap_handle = GCHandle.Alloc(vertices_cap, GCHandleType.Pinned);
            vertices_cap_pointer = vertices_cap_handle.AddrOfPinnedObject();

        }

        private void CalculateQuadMesh()
        {
            vertices_quad = new[]{-QUAD_OVERLAP_FUDGE, -1, 0,
                            1 + QUAD_OVERLAP_FUDGE, -1, 0,
                            -QUAD_OVERLAP_FUDGE, QUAD_MIDDLECRACK_FUDGE, 1,
                            1 + QUAD_OVERLAP_FUDGE, QUAD_MIDDLECRACK_FUDGE, 1,
                            -QUAD_OVERLAP_FUDGE, 1, 0,
                            1 + QUAD_OVERLAP_FUDGE, 1, 0};

            coordinates_quad = new float[COLOUR_COUNT][];
            coordinates_quad_handle = new GCHandle[COLOUR_COUNT];
            coordinates_quad_pointer = new IntPtr[COLOUR_COUNT];

            for (int x = 0; x < COLOUR_COUNT; x++)
            {
                float y = (2.0f + 4.0f * x + sheetY + TEXEL_ORIGIN) / retinaHeight;

                coordinates_quad[x] = new[]{sheetStart, y,
                                            sheetStart, y,
                                            sheetEnd, y,
                                            sheetEnd, y,
                                            sheetStart, y,
                                            sheetStart, y};

                coordinates_quad_handle[x] = GCHandle.Alloc(coordinates_quad[x], GCHandleType.Pinned);
                coordinates_quad_pointer[x] = coordinates_quad_handle[x].AddrOfPinnedObject();
            }


            vertices_quad_handle = GCHandle.Alloc(vertices_quad, GCHandleType.Pinned);
            vertices_quad_pointer = vertices_quad_handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Draws a slider to the active device using a cached texture.
        /// </summary>
        /// <param name="lineList">List of lines to use</param>
        /// <param name="radius">Width of the slider</param>
        /// <param name="ColourIndex">Current combo colour index between 0 and 4; -1 for grey; -2 for Tag Multi override.</param>
        /// <param name="prev">The last line which was rendered in the previous iteration, or null if this is the first iteration.</param>
        internal void Draw(List<Line> lineList, float radius, int ColourIndex, Line prev)
        {
            GL.Color4((byte)255, (byte)255, (byte)255, (byte)255);

            switch (ColourIndex)
            {
                /*case -1: // Grey
                    DrawOGL(lineList, radius, grey_ogl, prev);
                    break;
                case -2: // Multi custom
                    DrawOGL(lineList, radius, multi_ogl, prev);
                    break;*/
                default:
                    DrawOGL(lineList, radius, prev, true, ColourIndex);
                    break;
            }
        }

        internal void Initialize()
        {
            if (!boundEvents)
            {
                GameBase.OnScreenLayoutChanged += GameBase_OnScreenLayoutChanged;
                boundEvents = true;
            }

            Init();
        }

        #region IDisposable Members

        public void Dispose()
        {
            for (int i = 0; i < COLOUR_COUNT; i++)
            {
                coordinates_cap_handle[i].Free();
                coordinates_quad_handle[i].Free();
            }

            vertices_cap_handle.Free();
            vertices_quad_handle.Free();

            GameBase.OnScreenLayoutChanged -= GameBase_OnScreenLayoutChanged;
        }

        void GameBase_OnScreenLayoutChanged()
        {
            Initialize();
        }

        #endregion

        protected void glDrawQuad(int ColourIndex)
        {
            GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, coordinates_quad_pointer[ColourIndex]);
            GL.VertexPointer(3, VertexPointerType.Float, 0, vertices_quad_pointer);
            GL.DrawArrays(BeginMode.TriangleStrip, 0, 6);
        }

        protected void glDrawHalfCircle(int count, int ColourIndex)
        {
            GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, coordinates_cap_pointer[ColourIndex]);
            GL.VertexPointer(3, VertexPointerType.Float, 0, vertices_cap_pointer);

            GL.DrawArrays(BeginMode.TriangleFan, 0, count + 2);
        }

        /// <summary>
        /// Core drawing method in OpenGL
        /// </summary>
        /// <param name="lineList">List of lines to use</param>
        /// <param name="globalRadius">Width of the slider</param>
        /// <param name="texture">Texture used for the track</param>
        /// <param name="prev">The last line which was rendered in the previous iteration, or null if this is the first iteration.</param>
        protected void DrawOGL(List<Line> lineList, float globalRadius, Line prev, bool renderingToTexture, int ColourIndex)
        {
            if (renderingToTexture)
            {
                GL.Disable(EnableCap.Blend);
                GL.DepthMask(true);
                GL.DepthFunc(DepthFunction.Lequal);
                GL.Enable(EnableCap.DepthTest);
            }

            SpriteManager.TexturesEnabled = true;

            GL.MatrixMode(MatrixMode.Modelview);

            pTexture texture = TextureManager.Load(OsuTexture.tracks);
            texture.TextureGl.Bind();

            int count = lineList.Count;
            for (int x = 1; x < count; x++)
            {
                DrawLineOGL(prev, lineList[x - 1], lineList[x], globalRadius, ColourIndex);
                prev = lineList[x - 1];
            }

            if (count > 0)
                DrawLineOGL(prev, lineList[count - 1], null, globalRadius, ColourIndex);

            if (renderingToTexture)
            {
                GL.Enable(EnableCap.Blend);
                GL.Disable(EnableCap.DepthTest);
                GL.DepthMask(false);
            }
        }

        protected void DrawLineOGL(Line prev, Line curr, Line next, float globalRadius, int ColourIndex)
        {
            // Quad
            Matrix4 matrix = new Matrix4(curr.rho, 0, 0, 0, // Scale-X
                                        0, globalRadius, 0, 0, // Scale-Y
                                        0, 0, 1, 0,
                                        0, 0, 0, 1) * curr.WorldMatrix();

            GL.LoadMatrix(new float[]{matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                                    matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                                    matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                                    matrix.M41, matrix.M42, matrix.M43, matrix.M44});

            glDrawQuad(ColourIndex);

            int end_triangles;
            bool flip;
            if (next == null)
            {
                flip = false; // totally irrelevant
                end_triangles = numPrimitives_cap;
            }
            else
            {
                float theta = next.theta - curr.theta;

                // keep on the +- pi/2 range.
                if (theta > Math.PI) theta -= (float)(Math.PI * 2);
                if (theta < -Math.PI) theta += (float)(Math.PI * 2);

                if (theta < 0)
                {
                    flip = true;
                    end_triangles = (int)Math.Ceiling((-theta) * MAXRES / Math.PI + WEDGE_COUNT_FUDGE);
                }
                else if (theta > 0)
                {
                    flip = false;
                    end_triangles = (int)Math.Ceiling(theta * MAXRES / Math.PI + WEDGE_COUNT_FUDGE);
                }
                else
                {
                    flip = false; // totally irrelevant
                    end_triangles = 0;
                }
            }
            end_triangles = Math.Min(end_triangles, numPrimitives_cap);

            // Cap on end
            if (flip)
            {
                matrix = new Matrix4(globalRadius, 0, 0, 0,
                                    0, -globalRadius, 0, 0,
                                    0, 0, 1, 0,
                                    0, 0, 0, 1) * curr.EndWorldMatrix();

                GL.LoadMatrix(new float[]{matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                                    matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                                    matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                                    matrix.M41, matrix.M42, matrix.M43, matrix.M44});

            }
            else
            {
                matrix = new Matrix4(globalRadius, 0, 0, 0,
                                    0, globalRadius, 0, 0,
                                    0, 0, 1, 0,
                                    0, 0, 0, 1) * curr.EndWorldMatrix();

                GL.LoadMatrix(new float[]{matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                                    matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                                    matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                                    matrix.M41, matrix.M42, matrix.M43, matrix.M44});
            }

            glDrawHalfCircle(end_triangles, ColourIndex);

            // Cap on start
            bool hasStartCap = false;

            if (prev == null) hasStartCap = true;
            else if (curr.p1 != prev.p2) hasStartCap = true;

            //todo: this makes stuff look bad... need to look into it.
            if (hasStartCap)
            {
                // Catch for Darrinub and other slider inconsistencies. (Redpoints seem to be causing some.)
                // Render a complete beginning cap if this Line isn't connected to the end of the previous line.

                matrix = new Matrix4(-globalRadius, 0, 0, 0,
                                    0, -globalRadius, 0, 0,
                                    0, 0, 1, 0,
                                    0, 0, 0, 1) * curr.WorldMatrix();

                GL.LoadMatrix(new float[]{matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                                    matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                                    matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                                    matrix.M41, matrix.M42, matrix.M43, matrix.M44});

                glDrawHalfCircle(numPrimitives_cap, ColourIndex);
            }
        }
    }

}