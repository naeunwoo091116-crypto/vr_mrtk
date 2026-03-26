// Simple transparent shader.

Shader "Custom/UITransparent" {
Properties {
    _MainTex ("Base (RGB)", 2D) = "black" {}
	_Tint ("Color Tint", Color) = (1.0,1.0,1.0,1.0)
}

SubShader {
	Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
	Pass {
		Cull Off
		ZWrite Off
		Lighting Off
		Blend One One

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 2.0

		#include "UnityCG.cginc"

		sampler2D _MainTex;
		float4 _MainTex_ST;
		fixed4 _Tint;

		struct appdata {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f {
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		v2f vert (appdata v) {
			v2f o;
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			o.pos = UnityObjectToClipPos(v.vertex);
			o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			return o;
		}

		fixed4 frag (v2f i) : SV_Target {
			return tex2D(_MainTex, i.uv) * _Tint;
		}
		ENDCG
	}
}
}
