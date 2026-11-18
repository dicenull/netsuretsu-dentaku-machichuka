Shader "SpinningFeast/Lit"
{
 Properties { _Tint ("Tint", Color) = (1,1,1,1) _Gold ("Gold", Range(0,1)) = 0 _Glow ("Glow", Range(0,1)) = 0 _Gloss ("Gloss", Range(0,1)) = .5 _Specular ("Specular", Range(0,1)) = 0 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   CGPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   // Runtime-created materials need both variants retained in player builds.
   #pragma multi_compile_local __ FEAST_NO_SPECULAR
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f {
    float4 pos:SV_POSITION;
    float3 normal:TEXCOORD0;
#ifndef FEAST_NO_SPECULAR
    float3 world:TEXCOORD1;
#endif
    fixed4 color:COLOR;
   };
   fixed4 _Tint; float _Glow, _Gloss, _Specular, _Gold;
   v2f vert(appdata v) {
    UNITY_SETUP_INSTANCE_ID(v);
    v2f o;
    o.pos=UnityObjectToClipPos(v.vertex);
    o.normal=UnityObjectToWorldNormal(v.normal);
#ifndef FEAST_NO_SPECULAR
    o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
#endif
    o.color=v.color;
    return o;
   }
   fixed4 frag(v2f i):SV_Target {
    float3 n=normalize(i.normal),l=normalize(float3(-.4,1,-.7));
    float light=.45+.55*saturate(dot(n,l));
    float3 c=lerp(i.color.rgb*_Tint.rgb,float3(1,.65,.075)*(.72+.28*dot(i.color.rgb,float3(.3,.59,.11))),_Gold);
    float3 lit=c*light;
#ifndef FEAST_NO_SPECULAR
    float3 v=normalize(_WorldSpaceCameraPos-i.world);
    float spec=pow(saturate(dot(n,normalize(l+v))),lerp(12,80,_Gloss))*_Specular*i.color.a;
    lit+=spec*lerp(float3(1,.94,.8),float3(1,.83,.32),_Gold);
#endif
    return fixed4(lit+_Glow*_Tint.rgb*.5,1);
   }
   ENDCG
  }
 }
}
