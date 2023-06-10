Shader "Unlit/CardsSecretRare"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _CardMaskTex("Mask", 2D) = "white" {}
        _CardMaskTitle("Text Mask", 2D) = "white" {}
        _Brightness("Brightness", Float) = 1.0
        _Quality("Quality", Float) = 1.0
        _HoloScale("Holo Scale", Float) = 100.0
        _HoloBrightness("Holo Brightness", Float) = 1.0
        _HoloBlendMultiplier("Holo Blend Multi", Float) = 1.0
        _HoloNoiseScale("Holo Noise Scale", Float) = 1.0
        _ReflectionAngle("Reflection Angle", Float) = 0.0
        _TextGreyThreshold("Text Mask Threshold", Float) = 0.5
        _TextColour("Text Colour", Color) = (1,1,1,1)
        _TextWaveScale("Text Wave Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "CardShaderUtil.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _CardMaskTex;
            sampler2D _CardMaskTitle;
            float4 _MainTex_ST;
            float _Brightness;
            float _Quality;
            float _HoloScale;
            float _HoloBrightness;
            float _HoloBlendMultiplier;
            float _HoloNoiseScale;
            float _ReflectionAngle;
            float _TextGreyThreshold;
            float4 _TextColour;
            float _TextWaveScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv) * _Brightness;

                //col = smoothstep( 0.5, 1.0, i.uv.x ) * 

                //float _ReflectionAngle = sin(iTime) - 1.0f;

                float rainbow_uv_a = 1.0f - i.uv.x + i.uv.y;
                float rainbow_uv = frac((rainbow_uv_a * _HoloScale) / 2.0f);
                float3 spectral = spectral_zucconi6(frac(rainbow_uv_a * _HoloNoiseScale + _ReflectionAngle));
                float wave = sin(rainbow_uv * 100.f + noise(i.uv * float2(50.0f, 5000.0f)) * 1.5f) + 0.5f;
                float n = pow(noise(i.uv * 100.0f), 2.0f) * _HoloBrightness;
                n += 0.5f;
                wave *= n / 2.0f;
                wave += 0.5f;
                float4 rainbow = float4(spectral * wave, 1.0);

                // Title
                if (tex2D(_CardMaskTitle, i.uv).x >= 0.9f && (col.x + col.y + col.z) <= _TextGreyThreshold)
                {
                    //col = Blend(_TextColour, col, pow((col.r + col.b + col.b) / 3.0, 0.1));
                    //float interp = (0.5f + (col.x + col.y + col.z / 3.0f));
                    float text_wave = sin(_ReflectionAngle + rainbow_uv_a * _TextWaveScale) / 2.0f + 0.5f;
                    float3 text_rainbow = spectral_zucconi6(text_wave);
                    float f_abs_threshold = 0.4;
                    text_rainbow.x = pow(abs(text_rainbow.x - f_abs_threshold) + f_abs_threshold, 1.0);
                    text_rainbow.y = pow(abs(text_rainbow.y - f_abs_threshold) + f_abs_threshold, 1.0);
                    text_rainbow.z = pow(abs(text_rainbow.z - f_abs_threshold) + f_abs_threshold, 1.0);
                    col = Blend(_TextColour, float4(text_rainbow, 1.0f), text_wave);
                    //col = Blend(col, rainbow, (col.r + col.b + col.b) / 3.0);
                }

                float4 mask = tex2D(_CardMaskTex, i.uv);
                col = col * (1.0 - mask.x) + Blend(col, rainbow, (col.r + col.b + col.b) / 3.0 * _HoloBlendMultiplier) * mask.x;

                return col;
            }
            ENDCG
        }
    }
}