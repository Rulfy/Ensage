using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ensage;
using SharpDX;
using SharpDX.Direct3D9;

namespace TowerRange
{
    // Taken from https://github.com/LeagueSharp/LeagueSharp.Common/blob/master/Render.cs with minor changes until we have our own common library. All credits to the respective authors.
    class Circle
    {
        private static VertexBuffer _vertices;
        private static VertexElement[] _vertexElements;
        private static VertexDeclaration _vertexDeclaration;
        private static Effect _effect;
        private static EffectHandle _technique;
        private static bool _initialized;
        private static Vector3 _offset = new Vector3(0, 0, 0);

        public static void DrawCircle(Vector3 position, float radius, Color color, int width)
        {
            try
            {
                if (_vertices == null)
                {
                    Initialize();
                }
                if (_vertices == null || _vertices.IsDisposed || _vertexDeclaration.IsDisposed || _effect.IsDisposed || _technique.IsDisposed)
                {
                    return;
                }
                var device = Drawing.Direct3DDevice;
                var olddec = device.VertexDeclaration;
                _effect.Technique = _technique;
                _effect.Begin();
                _effect.BeginPass(0);
                _effect.SetValue("ProjectionMatrix", Matrix.Translation(position) * Drawing.View * Drawing.Projection);
                _effect.SetValue("CircleColor", new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f));
                _effect.SetValue("Radius", radius);
                _effect.SetValue("Border", 2f + width);
                _effect.SetValue("zEnabled", false);
                device.SetStreamSource(0, _vertices, 0, Utilities.SizeOf<Vector4>() * 2);
                device.VertexDeclaration = _vertexDeclaration;
                device.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);
                _effect.EndPass();
                _effect.End();
                device.VertexDeclaration = olddec;
            }
            catch (Exception e)
            {
                _vertices = null;
                Console.WriteLine(@"DrawCircle: " + e);
            }
        }

        private static void Initialize()
        {
            const float x = 6000f;
            _vertices = new VertexBuffer(
            Drawing.Direct3DDevice, Utilities.SizeOf<Vector4>() * 2 * 6, Usage.WriteOnly, VertexFormat.None, Pool.Managed);
            _vertices.Lock(0, 0, LockFlags.None).WriteRange(
                new[]
                {
                    //T1
                    new Vector4(-x, 0f, -x, 1.0f), new Vector4(), new Vector4(-x, 0f, x, 1.0f), new Vector4(),
                    new Vector4(x, 0f, -x, 1.0f), new Vector4(),
                    //T2
                    new Vector4(x, 0f, x, 1.0f), new Vector4(), new Vector4(-x, 0f, x, 1.0f), new Vector4(),
                    new Vector4(x, 0f, -x, 1.0f), new Vector4()
                });
            _vertices.Unlock();
            _vertexElements = new[]
            {
                new VertexElement(
                    0, 0, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(
                    0, 16, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Color, 0),
                VertexElement.VertexDeclarationEnd
            };
            _vertexDeclaration = new VertexDeclaration(Drawing.Direct3DDevice, _vertexElements);


            try
            {
                _effect = Effect.FromString(Drawing.Direct3DDevice, @"
                    struct VS_S
                    {
                        float4 Position : POSITION;
                        float4 Color : COLOR0;
                        float4 Position3D : TEXCOORD0;
                    };
                    float4x4 ProjectionMatrix;
                    float4 CircleColor;
                    float Radius;
                    float Border;
                    bool zEnabled;
                    VS_S VS( VS_S input )
                    {
                        VS_S output = (VS_S)0;
                        output.Position = mul(input.Position, ProjectionMatrix);
                        output.Color = input.Color;
                        output.Position3D = input.Position;
                        return output;
                    }
                    float4 PS( VS_S input ) : COLOR
                    {
                        VS_S output = (VS_S)0;
                        output = input;
                        float4 v = output.Position3D;
                        float distance = Radius - sqrt(v.x * v.x + v.z*v.z); // Distance to the circle arc.
                        output.Color.x = CircleColor.x;
                        output.Color.y = CircleColor.y;
                        output.Color.z = CircleColor.z;
                        if(distance < Border && distance > -Border)
                        {
                            output.Color.w = (CircleColor.w - CircleColor.w * abs(distance * 1.75 / Border));
                        }
                        else
                        {
                            output.Color.w = 0;
                        }
                        if(Border < 1 && distance >= 0)
                        {
                            output.Color.w = CircleColor.w;
                        }
                        return output.Color;
                    }
                    technique Main {
                        pass P0 {
                            ZEnable = zEnabled;
                            AlphaBlendEnable = TRUE;
                            DestBlend = INVSRCALPHA;
                            SrcBlend = SRCALPHA;
                            VertexShader = compile vs_2_0 VS();
                            PixelShader = compile ps_2_0 PS();
                        }
                    }"
                    , ShaderFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return;
            }

            _technique = _effect.GetTechnique(0);
            if (!_initialized)
            {
                Drawing.OnPreReset += Drawing_OnPreReset;
                Drawing.OnPostReset += Drawing_OnPostReset;
                AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
                _initialized = true;
            }
        }

        static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            if (_effect != null && !_effect.IsDisposed)
            {
                _effect.Dispose();
            }
            if (_vertices != null && !_vertices.IsDisposed)
            {
                _vertices.Dispose();
            }
            if (_vertexDeclaration != null && !_vertexDeclaration.IsDisposed)
            {
                _vertexDeclaration.Dispose();
            }
        }

        static void Drawing_OnPostReset(EventArgs args)
        {
            if (_effect != null && !_effect.IsDisposed)
            {
                _effect.OnResetDevice();
            }
        }

        static void Drawing_OnPreReset(EventArgs args)
        {
            if (_effect != null && !_effect.IsDisposed)
            {
                _effect.OnLostDevice();
            }
        }
    }
}
