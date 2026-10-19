Shader "UI/SidePanelGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // hex 直接填 Inspector，面板自动走 sRGB -> Linear
        _EdgeColor   ("Edge Color",   Color) = (0.0353, 0.0392, 0.0471, 1)
        _CenterColor ("Center Color", Color) = (0.1608, 0.1608, 0.2078, 1)

        // 光心位置，偏左上通常比正中好看
        _Center  ("Center Pos", Vector) = (0.5, 0.5, 0, 0)

        // 面板 宽/高，手填。竖条不补正光斑会被拉扁
        _Aspect  ("Aspect W/H", Float) = 0.5

        // >1 光斑收紧，<1 散开
        _Falloff ("Falloff", Range(0.3, 3)) = 1.6

        // 左上角斜切掉的宽度
        _Slant   ("Slant", Range(0, 0.3)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _EdgeColor;
            float4 _CenterColor;
            float4 _Center;

            float _Aspect;
            float _Falloff;
            float _Slant;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, i.uv);

                // 径向距离：中心 0，边缘 1
                float2 d = i.uv - _Center.xy;
                d.x *= _Aspect;
                float r = saturate(length(d) * 2.0);

                // 取幂拉出中心光斑，线性衰减看着像脏
                r = pow(r, _Falloff);

                fixed4 result;
                result.rgb = lerp(_CenterColor.rgb, _EdgeColor.rgb, r);
                // 左上角斜切，0.02 是边缘羽化宽度
                float slantEdge = _Slant * i.uv.y;
                float slantMask = smoothstep(slantEdge, slantEdge + 0.02, i.uv.x);

                result.a =
                    textureColor.a *
                    i.color.a *
                    slantMask;

                return result;
            }

            ENDCG
        }
    }
}
