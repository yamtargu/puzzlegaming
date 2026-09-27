// Board star drawn as a signed distance field on a quad (Art.StarQuad), so it stays crisp at any size.
// Shape: N slim arms with slightly concave edges and rounded tips. Light: white-hot core fading to warm
// gold tips, a thin bright rim, a soft halo, and a 4-point glint flare.
// The "pirouette" (tilt onto a tip, spin around the vertical axis, hop, squash) happens here in the
// quad's own space, so the star's transform, hit area, shadow and light never move.
// Per-star values come from a MaterialPropertyBlock (Node.cs); the look comes from StarStyle.
Shader "OneLine/StarSDF"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}

        [Header(Shape)]
        _Points ("Points", Range(3, 8)) = 5
        _InnerRatio ("Inner / outer radius", Range(0.2, 0.7)) = 0.4
        _TipRound ("Tip roundness", Range(0, 0.25)) = 0.05
        _EdgeCurve ("Edge concavity", Range(0, 0.25)) = 0.07

        [Header(Light)]
        _CoreColor ("Core color", Color) = (1, 0.98, 0.92, 1)
        _TipColor ("Tip color", Color) = (1, 0.76, 0.34, 1)
        _CoreSize ("Core size", Range(0.05, 1)) = 0.38
        _Falloff ("Core to tip falloff", Range(0.3, 4)) = 1.3
        _CoreBoost ("Core boost", Range(0, 1)) = 0.25
        _RimColor ("Rim color", Color) = (1, 0.93, 0.72, 1)
        _RimWidth ("Rim width", Range(0.005, 0.3)) = 0.07
        _RimIntensity ("Rim intensity", Range(0, 2)) = 0.7

        [Header(Glow)]
        _GlowColor ("Glow color", Color) = (1, 0.8, 0.45, 1)
        _GlowRadius ("Glow radius", Range(0.2, 3)) = 1.2
        _GlowIntensity ("Glow intensity", Range(0, 2)) = 0.4
        _GlowAlpha ("Glow opacity (0 = pure additive)", Range(0, 1)) = 0.15
        _BreatheGlow ("Breathe glow", Range(0, 2)) = 0.5

        [Header(Glint)]
        _GlintColor ("Glint color", Color) = (1, 0.96, 0.85, 1)
        _GlintIntensity ("Glint intensity", Range(0, 3)) = 1.2
        _GlintLength ("Glint length", Range(0.5, 3)) = 2.4
        _GlintWidth ("Glint width", Range(0.005, 0.2)) = 0.045
        _SheenIntensity ("Spin sheen", Range(0, 1.5)) = 0.5
        _SpinMinWidth ("Spin min width", Range(0, 0.5)) = 0.08

        _Canvas ("Quad half-size, in star radii", Float) = 3

        // Per-star animation (MaterialPropertyBlock):
        // _Anim  = (breathe 0..1, glint 0..1, tilt radians, spin radians)
        // _Anim2 = (hop, squash, spin direction, tint amount)
        // _Anim3 = (glow flash, glint angle radians, -, -)
        [HideInInspector] _Anim ("Anim", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Anim2 ("Anim2", Vector) = (0, 0, 1, 0.8)
        [HideInInspector] _Anim3 ("Anim3", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False"
        }
        Blend One OneMinusSrcAlpha // premultiplied: the halo and glint add light
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Points, _InnerRatio, _TipRound, _EdgeCurve;
                half4 _CoreColor, _TipColor, _RimColor, _GlowColor, _GlintColor;
                float _CoreSize, _Falloff, _CoreBoost, _RimWidth, _RimIntensity;
                float _GlowRadius, _GlowIntensity, _GlowAlpha, _BreatheGlow;
                float _GlintIntensity, _GlintLength, _GlintWidth, _SheenIntensity, _SpinMinWidth;
                float _Canvas;
                float4 _Anim, _Anim2, _Anim3;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 p : TEXCOORD0; // quad position in star radii, star center = 0
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color;
                o.p = (v.uv - 0.5) * 2.0 * _Canvas;
                return o;
            }

            float2 Rotate(float2 v, float a)
            {
                float s, c;
                sincos(a, s, c);
                return float2(v.x * c - v.y * s, v.x * s + v.y * c);
            }

            // Signed distance (negative inside) to a star with outer radius 1, first tip pointing up.
            float StarDistance(float2 q)
            {
                float n = max(3.0, round(_Points));
                float an = PI / n;
                // Fold into one half-arm: tip on +x, valley on the ray at angle an.
                float a = atan2(q.x, q.y);
                a -= 2.0 * an * round(a / (2.0 * an));
                float2 f = length(q) * float2(cos(a), abs(sin(a)));

                float2 v = _InnerRatio * float2(cos(an), sin(an));
                // Pull the tip in so the rounded tip still reaches radius 1.
                float tipHalf = atan2(v.y, 1.0 - v.x);
                float2 t = float2(1.0 - _TipRound / max(sin(tipHalf), 0.05), 0.0);

                float2 e = v - t;
                float len = length(e);
                float2 dir = e / len;
                float2 nOut = float2(dir.y, -dir.x); // away from the center
                float s = dot(f - t, dir) / len;     // 0 at the tip, 1 at the valley

                float d;
                if (_EdgeCurve < 0.002)
                {
                    d = dot(f - t, nOut); // straight edge
                }
                else
                {
                    // Concave edge: circular arc bowing inward by edgeCurve * chord length.
                    float h = _EdgeCurve * len;
                    float rad = (len * len * 0.25 + h * h) / (2.0 * h);
                    float2 c = (t + v) * 0.5 + nOut * (rad - h);
                    d = rad - length(f - c);
                }
                if (s < 0.0) d = length(f - t);                  // past the tip: round cap
                else if (s > 1.0) d = sign(d) * length(f - v);   // past the valley
                return d - _TipRound;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float breathe = _Anim.x, glint = _Anim.y, tilt = _Anim.z, spin = _Anim.w;
                float hop = _Anim2.x, squash = _Anim2.y, spinDir = _Anim2.z, tintAmount = _Anim2.w;
                float flash = _Anim3.x, glintAngle = _Anim3.y;
                half4 tint = i.color;

                // Undo the pirouette: hop up, squash about the foot, spin (x scale), tilt.
                float2 p = i.p;
                float2 q = p - float2(0.0, hop);
                const float2 foot = float2(0.0, -1.0);
                q = foot + (q - foot) / float2(1.0 + 0.5 * squash, 1.0 - squash);
                float c = cos(spin);
                float sx = (c >= 0.0 ? 1.0 : -1.0) * max(abs(c), _SpinMinWidth);
                q.x /= sx;
                q = Rotate(q, -tilt);

                float d = StarDistance(q);
                float aa = max(fwidth(d), 1e-4);
                float body = saturate(0.5 - d / aa);

                // Color: white-hot core -> tip color (pulled toward the star's tint).
                float r = length(q);
                half3 tipColor = lerp(_TipColor.rgb, tint.rgb, tintAmount);
                half3 col = lerp(tipColor, _CoreColor.rgb, pow(saturate(1.0 - r), _Falloff));
                col += _CoreColor.rgb * _CoreBoost * exp(-r * r / (_CoreSize * _CoreSize));
                // Thin bright rim just inside the edge.
                float rim = saturate(1.0 + d / _RimWidth) * body;
                col += lerp(_RimColor.rgb, tint.rgb, tintAmount * 0.5) * _RimIntensity * rim * rim;
                // Sheen sweeping across while spinning, so the turn direction reads.
                float sweep = sin(spin);
                float sheen = (p.x + spinDir * sweep * 0.8) / 0.3;
                col += _CoreColor.rgb * _SheenIntensity * abs(sweep) * exp(-sheen * sheen) * body;

                // Halo: round soft light plus a star-shaped inner glow. Stays upright while the star spins.
                float2 hp = p - float2(0.0, hop * 0.5);
                float glowLevel = _GlowIntensity * (1.0 + _BreatheGlow * breathe + flash);
                float halo = glowLevel * (0.6 * exp(-dot(hp, hp) / (_GlowRadius * _GlowRadius))
                                        + 0.4 * exp(-max(d, 0.0) / (_GlowRadius * 0.35)));
                half3 glowColor = lerp(_GlowColor.rgb, tint.rgb, tintAmount);

                // Glint: thin 4-point cross flare with a bright center.
                float2 gp = Rotate(p - float2(0.0, hop), glintAngle);
                float2 along = saturate(1.0 - abs(gp) / _GlintLength);
                float arms = exp(-abs(gp.y) / _GlintWidth) * along.x * along.x
                           + exp(-abs(gp.x) / _GlintWidth) * along.y * along.y;
                float flare = glint * _GlintIntensity * (arms + 0.8 * exp(-dot(gp, gp) / 0.1));

                float bodyAlpha = body * tint.a;
                float light = (halo * (1.0 - body) + flare) * tint.a;
                half3 rgb = col * bodyAlpha + glowColor * halo * (1.0 - body) * tint.a + _GlintColor.rgb * flare * tint.a;
                return half4(rgb, saturate(bodyAlpha + light * _GlowAlpha));
            }
            ENDHLSL
        }
    }
}
