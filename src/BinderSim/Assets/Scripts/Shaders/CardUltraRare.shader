Shader "Unlit/CardUltraRare"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _CardMaskTex("Mask", 2D) = "white" {}
        _CardMaskTitle("Text Mask", 2D) = "white" {}
        _Brightness("Brightness", Float) = 1.0
        _Quality("Quality", Float) = 1.0
        _HoloScale("Holo Scale", Float) = 1.0
        _HoloBrightness("Holo Brightness", Float) = 1.0
        _HoloNoiseScale("Holo Noise Scale", Float) = 1.0
        _ReflectionAngle("Reflection Angle", Float) = 0.0
        _TextGreyThreshold("Text Mask Threshold", Float) = 0.5
        _TextColour("Text Colour", Color) =  (1,1,1,1)
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
            float _HoloNoiseScale;
            float _ReflectionAngle;
            float _TextGreyThreshold;
            float4 _TextColour;

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


                float rainbow_uv = i.uv.x;

                rainbow_uv = frac((rainbow_uv * _HoloScale) / 2.0 + _ReflectionAngle);
                float4 rainbow = float4(spectral_zucconi6(rainbow_uv), 1.0);
                float4 mask = tex2D(_CardMaskTex, i.uv);
                //rainbow *= mask;
                rainbow *= _HoloBrightness;

                col = col * (1.0 - mask.x) + Blend(col, rainbow, (col.r + col.g + col.b) / 3.0) * mask.x;
                //float blend = 0.5;
                //col = col * ( 1.0 - mask.x ) + Blend(col, rainbow, blend ) * mask.x;
                //col = Star( ( i.uv - float2(0.5, 0.5) ) * 10.0, 0.1 );

                // Title
                if (tex2D(_CardMaskTitle, i.uv).x >= 0.9f && (col.x + col.y + col.z) <= _TextGreyThreshold)
                {
                    //col = Blend(_TextColour, col, pow((col.r + col.b + col.b) / 3.0, 0.1));
                    //float interp = (0.5f + (col.x + col.y + col.z / 3.0f));
                    col = _TextColour;// *float4(interp, interp, interp, interp);
                    col = Blend(col, rainbow, (col.r + col.b + col.b) / 3.0);
                }

                return col;
            }
            ENDCG
        }
    }
}
