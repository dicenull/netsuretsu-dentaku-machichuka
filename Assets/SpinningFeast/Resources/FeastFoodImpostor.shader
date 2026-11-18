Shader "SpinningFeast/FoodImpostor"
{
 Properties { _MainTex ("Atlas", 2D) = "white" {} _Tint ("Tint", Color) = (1,1,1,1) _Glow ("Glow", Float) = 0 _Cutoff ("Cutout", Range(0,1)) = .35 }
 SubShader {
  Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
  Cull Off ZWrite On Blend Off
  Pass {
   CGPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   sampler2D _MainTex; fixed4 _Tint; float _Glow, _Cutoff;
   UNITY_INSTANCING_BUFFER_START(Food)
    UNITY_DEFINE_INSTANCED_PROP(float4, _AtlasRect)
   UNITY_INSTANCING_BUFFER_END(Food)
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata v) { UNITY_SETUP_INSTANCE_ID(v); v2f o; o.pos=UnityObjectToClipPos(v.vertex); float4 rect=UNITY_ACCESS_INSTANCED_PROP(Food,_AtlasRect); o.uv=v.uv*rect.xy+rect.zw; return o; }
   fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv); clip(c.a-_Cutoff); return fixed4(c.rgb*_Tint.rgb+_Glow*_Tint.rgb*.5,1); }
   ENDCG
  }
 }
}
