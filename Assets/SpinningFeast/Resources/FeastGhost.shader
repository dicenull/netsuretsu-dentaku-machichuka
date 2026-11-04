Shader "SpinningFeast/Ghost"
{
 Properties { _Tint ("Tint", Color) = (.3,1,1,.35) _DepthTest ("Depth test", Float) = 4 _ColorMix ("Food color", Range(0,1)) = 0 _Hologram ("Hologram", Range(0,1)) = 0 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off
  ZTest [_DepthTest]
  Pass {
   CGPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 view:TEXCOORD1; fixed4 color:COLOR; float height:TEXCOORD2; };
   fixed4 _Tint; float _ColorMix, _Hologram;
   v2f vert(appdata v) { UNITY_SETUP_INSTANCE_ID(v); v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.view=WorldSpaceViewDir(v.vertex); o.color=v.color; o.height=v.vertex.y; return o; }
   fixed4 frag(v2f i):SV_Target { float rim=pow(1-abs(dot(normalize(i.normal),normalize(i.view))),2); float scan=.88+.12*sin(i.height*65-_Time.y*16);
    float band=pow(saturate(1-abs(frac(i.height*.8-_Time.y*.6)*2-1)),12);
    float3 c=lerp(_Tint.rgb,i.color.rgb,_ColorMix)+_Tint.rgb*rim*.25+_Hologram*_Tint.rgb*band*.3;
    return fixed4(c,_Tint.a*(.65+.35*rim)*lerp(1,scan,_Hologram)); }
   ENDCG
  }
 }
}
