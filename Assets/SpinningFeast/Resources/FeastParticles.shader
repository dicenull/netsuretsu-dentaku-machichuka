Shader "SpinningFeast/Particles"
{
    Properties { _MainTex ("Particle atlas", 2D) = "white" {} _DstBlend ("Destination blend", Float) = 10 }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target {fixed4 tex=tex2D(_MainTex,i.uv);return fixed4(i.color.rgb,tex.a*i.color.a);}
            ENDCG
        }
    }
}
