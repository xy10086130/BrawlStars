// 草丛里的"X 光剪影"：忽略深度遮挡 + 半透明，让玩家透过草看见自己。
// 由 GrassGhost.cs 在运行时 new Material(此 shader) 使用，英雄原始材质出草时会换回来。
//
// ⚠ ZTest Always 是这里唯一的核心：草是不透明几何体、已经先写了深度，渲染队列排在它后面
//    也照样会被 LEqual 剔掉（URP/Lit 的 ZTest 是硬编码的，材质上没有 _ZTest 可调）。
//
// ⚠ 别把这个 shader 挂成常驻材质：queue 3000 > 草 2000 → 英雄会永远画在草上面，草丛遮挡直接消失。
//
// ponytail: 没写 ShadowCaster pass —— 草里那几秒不投影（剪影状态几乎看不出）。要补再加。
Shader "Brawl/GrassGhost"
{
    Properties
    {
        [MainColor] _Color("Tint", Color) = (1, 1, 1, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Back

        Pass
        {
            Name "Ghost"
            // 显式写出来：URP 的 forward 列表认 UniversalForward（不写也能被当 SRPDefaultUnlit 收，
            // 但显式一点省得以后有人以为这 pass 没生效）
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 不接光照：剪影要的就是平，接了反而会把草的颜色变化带进来
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _Color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
