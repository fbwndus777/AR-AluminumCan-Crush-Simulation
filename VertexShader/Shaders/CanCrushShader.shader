Shader "Custom/CanCrushShader"
{
    Properties
    {
        _Color      ("Color", Color) = (1,1,1,1)
        _MainTex    ("Albedo (RGB)", 2D) = "white" {}
        _Metallic   ("Metallic", Range(0,1)) = 0.85
        _Glossiness ("Smoothness", Range(0,1)) = 0.75
        _BumpMap    ("Normal Map", 2D) = "bump" {}
        _BumpScale  ("Normal Scale", Float) = 1.0

        _Crush0 ("Crush Zone 0", Vector) = (0, 0.5, 0, 0.3)
        _Crush1 ("Crush Zone 1", Vector) = (0, 0.5, 0, 0.3)
        _Crush2 ("Crush Zone 2", Vector) = (0, 0.5, 0, 0.3)
        _Crush3 ("Crush Zone 3", Vector) = (0, 0.5, 0, 0.3)
        _Crush4 ("Crush Zone 4", Vector) = (0, 0.5, 0, 0.3)
        _Crush5 ("Crush Zone 5", Vector) = (0, 0.5, 0, 0.3)
        _Crush6 ("Crush Zone 6", Vector) = (0, 0.5, 0, 0.3)
        _Crush7 ("Crush Zone 7", Vector) = (0, 0.5, 0, 0.3)

        _MaxCrushDepth ("Max Crush Depth (m)", Float) = 0.012
        _CanMinY ("Can Bottom Y (object space)", Float) = -0.065
        _CanMaxY ("Can Top Y (object space)", Float) = 0.065
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _BumpScale;

        float4 _Crush0, _Crush1, _Crush2, _Crush3;
        float4 _Crush4, _Crush5, _Crush6, _Crush7;
        float _MaxCrushDepth;
        float _CanMinY, _CanMaxY;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
        };

        float crushDisp(float theta, float hNorm, float4 zone)
        {
            float sev = zone.z;
            if (sev < 0.001) return 0.0;

            float dh = hNorm - zone.y;
            float sp = max(zone.w, 0.05);
            float axW = exp(-(dh * dh) / (sp * sp));

            float rel = theta - zone.x;
            rel = rel - 6.28318 * floor((rel + 3.14159) / 6.28318);
            float absRel = abs(rel);

            float delta = sev * 0.9;
            float foldAngle = acos(clamp(1.0 - delta, -1.0, 1.0));

            // Process only inside fold line, zero outside
            float inside = 1.0 - smoothstep(foldAngle * 0.85, foldAngle, absRel);
            float facetDepth = -(1.0 - cos(foldAngle));
            float d = facetDepth * inside;

            return d * axW;
        }

        float crushDeriv(float theta, float hNorm, float4 zone)
        {
            float eps = 0.002;
            float h1 = crushDisp(theta + eps, hNorm, zone);
            float h2 = crushDisp(theta - eps, hNorm, zone);
            return (h1 - h2) / (2.0 * eps);
        }

        void vert(inout appdata_full v)
        {
            float3 p = v.vertex.xyz;

            float r = length(float2(p.x, p.z));
            if (r < 0.0001) return;

            float theta = atan2(p.x, p.z);
            float canH = max(_CanMaxY - _CanMinY, 0.001);
            float hNorm = (p.y - _CanMinY) / canH;

            // Protect top and bottom caps from deformation
            float capMask = smoothstep(0.0, 0.3, hNorm) * smoothstep(1.0, 0.78, hNorm);

            float d = 0.0;
            d += crushDisp(theta, hNorm, _Crush0);
            d += crushDisp(theta, hNorm, _Crush1);
            d += crushDisp(theta, hNorm, _Crush2);
            d += crushDisp(theta, hNorm, _Crush3);
            d += crushDisp(theta, hNorm, _Crush4);
            d += crushDisp(theta, hNorm, _Crush5);
            d += crushDisp(theta, hNorm, _Crush6);
            d += crushDisp(theta, hNorm, _Crush7);

            float3 radDir = float3(p.x, 0, p.z) / r;
            float displacement = d * _MaxCrushDepth * capMask;
            displacement = clamp(displacement, -r * 0.88, r * 0.5);
            v.vertex.xyz += radDir * displacement;

            float ddt = 0.0;
            ddt += crushDeriv(theta, hNorm, _Crush0);
            ddt += crushDeriv(theta, hNorm, _Crush1);
            ddt += crushDeriv(theta, hNorm, _Crush2);
            ddt += crushDeriv(theta, hNorm, _Crush3);
            ddt += crushDeriv(theta, hNorm, _Crush4);
            ddt += crushDeriv(theta, hNorm, _Crush5);
            ddt += crushDeriv(theta, hNorm, _Crush6);
            ddt += crushDeriv(theta, hNorm, _Crush7);

            float3 tanDir = float3(-radDir.z, 0, radDir.x);
            float tiltAmount = ddt * _MaxCrushDepth * capMask / max(r, 0.001);
            v.normal = normalize(v.normal - tanDir * tiltAmount);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;

            // UV to cylindrical coordinates
            float theta = IN.uv_MainTex.x * 6.28318;
            float canH = max(_CanMaxY - _CanMinY, 0.001);
            float hNorm = IN.uv_MainTex.y; // Already assumed 0~1

            // Compute deformation gradient via numerical differentiation
            float eps = 0.03;
            float h0 = 0.0, hT = 0.0, hV = 0.0;

            h0 += crushDisp(theta,       hNorm,       _Crush0);
            h0 += crushDisp(theta,       hNorm,       _Crush1);
            h0 += crushDisp(theta,       hNorm,       _Crush2);
            h0 += crushDisp(theta,       hNorm,       _Crush3);
            h0 += crushDisp(theta,       hNorm,       _Crush4);
            h0 += crushDisp(theta,       hNorm,       _Crush5);
            h0 += crushDisp(theta,       hNorm,       _Crush6);
            h0 += crushDisp(theta,       hNorm,       _Crush7);

            hT += crushDisp(theta + eps, hNorm,       _Crush0);
            hT += crushDisp(theta + eps, hNorm,       _Crush1);
            hT += crushDisp(theta + eps, hNorm,       _Crush2);
            hT += crushDisp(theta + eps, hNorm,       _Crush3);
            hT += crushDisp(theta + eps, hNorm,       _Crush4);
            hT += crushDisp(theta + eps, hNorm,       _Crush5);
            hT += crushDisp(theta + eps, hNorm,       _Crush6);
            hT += crushDisp(theta + eps, hNorm,       _Crush7);

            hV += crushDisp(theta,       hNorm + eps, _Crush0);
            hV += crushDisp(theta,       hNorm + eps, _Crush1);
            hV += crushDisp(theta,       hNorm + eps, _Crush2);
            hV += crushDisp(theta,       hNorm + eps, _Crush3);
            hV += crushDisp(theta,       hNorm + eps, _Crush4);
            hV += crushDisp(theta,       hNorm + eps, _Crush5);
            hV += crushDisp(theta,       hNorm + eps, _Crush6);
            hV += crushDisp(theta,       hNorm + eps, _Crush7);

            float dh_dt = (hT - h0) / eps;
            float dh_dv = (hV - h0) / eps;

            // Deformation-based normal (high multiplier for sharp creases)
            float3 crushNormal = normalize(float3(-dh_dt * 4.0, -dh_dv * 4.0, 1.0));

            // Texture normal map
            float3 texNormal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            texNormal.xy *= _BumpScale;
            texNormal = normalize(texNormal);

            // Blend based on deformation magnitude
            float dispMag = abs(h0);
            float blendT = saturate(dispMag * 0.8);
            o.Normal = normalize(lerp(texNormal, crushNormal, blendT));

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Standard"
}