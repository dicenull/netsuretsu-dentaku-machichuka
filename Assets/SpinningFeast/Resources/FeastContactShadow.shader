Shader "SpinningFeast/ContactShadow"
{
 SubShader {
  Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off
  Cull Off
  Pass {
   CGPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata v) { UNITY_SETUP_INSTANCE_ID(v); v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o; }
   fixed4 frag(v2f i):SV_Target {float a=pow(saturate(1-dot(i.uv,i.uv)),2)*.08;return fixed4(.015,.025,.02,a);}
   ENDCG
  }
 }
}
