Shader "HighlightShader" {
	Properties{
		_Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
	_BumpMap("Bumpmap", 2D) = "bump" {}
	_RimColor("Rim Color", Color) = (0.26, 0.19, 0.16, 0)
		_RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
	}
		SubShader{
		Tags{ "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
#pragma target 3.0

		struct Input {
		float2 uv_MainTex;
		float2 uv_BumpMap;
		float3 viewDir;
	};

	float4 _MainColor;
	sampler2D _MainTex;
	sampler2D _BumpMap;
	float4 _RimColor;
	float _RimPower;



	void surf(Input IN, inout SurfaceOutputStandard o) {
		// Albedo comes from a texture tinted by color
		o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _MainColor.rgb;
		o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));

		half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
		o.Emission = _RimColor.rgb * pow(rim, _RimPower);
	}
	ENDCG
	}
		FallBack "Diffuse"
}
