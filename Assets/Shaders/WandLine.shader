// One drawn path segment (a LineRenderer in Stretch mode, no caps), drawn as a capsule distance field:
// an anti-aliased core with round ends and a soft glow in one pass, so segments meet cleanly at a star.
// The segment's geometry is extended by its half width at both ends (WandTrail); _Seg tells the shader
// the real length. Modes (WandEffect): 0 Classic, 1 Stardust (twinkling 4-point sparkles), 2 Rainbow (pastel
// hue flowing along the whole path), 3 Moonlight (soft bands of light sliding along it), 4 Comet (soft glowing
// trail), 5 Gold Ink (metallic ribbon whose width follows the finger's speed, with a sliding sheen).
// Values come from a WandLook; per-segment values from a property block.
Shader "OneLine/WandLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Mode ("Mode (WandEffect)", Float) = 0
        _LineColor ("Line color", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow color", Color) = (1, 1, 1, 1)
        _PathMix ("Path color mix", Range(0, 1)) = 1
        _CoreWidth ("Core width", Float) = 0.055
        _CoreSoftness ("Core softness", Range(0, 1)) = 0
        _GlowAlpha ("Glow alpha", Range(0, 1)) = 0.28
        _GlowOpacity ("Glow opacity", Range(0, 1)) = 0.6
        _CoreHighlight ("Core highlight", Range(0, 1)) = 0.35
        _SparkleSpacing ("Sparkle spacing", Float) = 0.2
        _SparkleSize ("Sparkle size", Float) = 0.09
        _SparkleRate ("Sparkle rate", Float) = 0.45
        _SparkleIntensity ("Sparkle intensity", Float) = 1.3
        _SparkleColor ("Sparkle color", Color) = (1, 1, 1, 1)
        _Hue ("Hue (cycles/unit, speed, saturation, value)", Vector) = (0.12, 0.06, 0.45, 1)
        _Shimmer ("Shimmer (amount, wavelength, speed, sharpness)", Vector) = (0.6, 1.6, 0.5, 6)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Linear ("Linear color space", Float) = 1
        // Per segment: (drawn length, half width, path distance at its start, full length)
        [HideInInspector] _Seg ("Segment", Vector) = (1, 0.14, 0, 1)
        // Per segment: core width multipliers at 0, 1/3, 2/3 and the end of the segment (Gold Ink)
        [HideInInspector] _SegWidth ("Segment widths", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True" "PreviewType" = "Plane"
        }
        Blend One OneMinusSrcAlpha // premultiplied: the glow can add light
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
                float _Mode, _PathMix, _CoreWidth, _CoreSoftness, _GlowAlpha, _GlowOpacity, _CoreHighlight;
                half4 _LineColor, _GlowColor, _SparkleColor;
                float _SparkleSpacing, _SparkleSize, _SparkleRate, _SparkleIntensity;
                float4 _Hue, _Shimmer;
                float _Metallic, _Linear;
                float4 _Seg, _SegWidth;
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
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            half3 HueColor(float pathDistance)
            {
                float h = frac(pathDistance * _Hue.x - _Time.y * _Hue.y);
                float3 k = saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                half3 c = _Hue.w * lerp(1.0, k, _Hue.z);
                return _Linear > 0.5 ? pow(max(c, 0.0), 2.2) : c; // same color as Color.HSVToRGB on the C# side
            }

            // Brightness of the bands of light sliding along the path (same formula as WandLook.Shimmer).
            float Shimmer(float pathDistance)
            {
                float phase = frac((pathDistance - _Time.y * _Shimmer.z) / max(_Shimmer.y, 1e-3));
                return _Shimmer.x * pow(saturate(0.5 + 0.5 * cos(phase * 6.2831853)), max(_Shimmer.w, 1.0));
            }

            // Short 4-point glints scattered along the drawn part of the path, each with its own phase and speed.
            float Sparkles(float s, float across, float s0, float s1)
            {
                float sum = 0.0;
                float cell = floor(s / _SparkleSpacing);
                [unroll] for (int k = -1; k <= 1; k++)
                {
                    float c = cell + k;
                    float pos = (c + 0.2 + 0.6 * Hash(c)) * _SparkleSpacing;
                    if (pos < s0 || pos > s1) continue;
                    float2 p = float2(s - pos, across - (Hash(c + 17.1) - 0.5) * _CoreWidth * 1.4);
                    float cycle = frac(_Time.y * _SparkleRate * (0.6 + 0.8 * Hash(c + 31.7)) + Hash(c + 47.3));
                    float flash = saturate(1.0 - abs(cycle - 0.08) / 0.08); // on ~16% of the time
                    flash *= flash;
                    float len = _SparkleSize, w = _SparkleSize * 0.07;
                    float2 ap = abs(p);
                    float arms = exp(-ap.y / w) * saturate(1.0 - ap.x / len) + exp(-ap.x / w) * saturate(1.0 - ap.y / len);
                    sum += (0.8 * arms + exp(-dot(p, p) / (w * w * 8.0))) * flash;
                }
                return sum * _SparkleIntensity;
            }

            half4 frag(Varyings i) : SV_Target
            {
                int mode = (int)round(_Mode);
                float drawn = _Seg.x, halfWidth = _Seg.y, start = _Seg.z, full = max(_Seg.w, 1e-4);
                float along = i.uv.x * (drawn + 2.0 * halfWidth) - halfWidth;
                float across = (i.uv.y - 0.5) * 2.0 * halfWidth;
                float dx = along - clamp(along, 0.0, drawn);
                float dist = length(float2(dx, across)); // distance to the segment: round ends for free

                // Core width along the segment (Gold Ink: thicker where the finger was slow).
                float x = saturate(along / full) * 3.0;
                float widthScale = x < 1.0 ? lerp(_SegWidth.x, _SegWidth.y, x)
                                 : x < 2.0 ? lerp(_SegWidth.y, _SegWidth.z, x - 1.0)
                                 : lerp(_SegWidth.z, _SegWidth.w, x - 2.0);
                float coreHalf = max(_CoreWidth * 0.5 * widthScale, 1e-4);
                float aa = max(fwidth(dist), 1e-5);
                float core = 1.0 - smoothstep(coreHalf * (1.0 - _CoreSoftness) - aa * 0.5, coreHalf + aa * 0.5, dist);
                float g = saturate(1.0 - dist / halfWidth);
                float glow = _GlowAlpha * g * g * (1.0 - core);

                float pathDistance = start + clamp(along, 0.0, drawn);
                half3 lineColor = _LineColor.rgb, glowColor = _GlowColor.rgb;
                if (mode == 2) lineColor = glowColor = HueColor(pathDistance);
                lineColor = lerp(lineColor, i.color.rgb, _PathMix);
                glowColor = lerp(glowColor, i.color.rgb, _PathMix);
                half3 coreColor = lerp(lineColor, 1.0, _CoreHighlight * saturate(1.0 - dist / coreHalf));

                if (mode == 5)
                {
                    // Metal ribbon: a bright ridge a little off center, darker edges.
                    float ridge = saturate(1.0 - abs(across / coreHalf + 0.3) / 1.3);
                    coreColor *= lerp(1.0, 0.5 + 0.8 * ridge, _Metallic);
                }
                if (mode == 3 || mode == 5)
                {
                    float shimmer = Shimmer(pathDistance);
                    coreColor += shimmer * lerp(lineColor, 1.0, 0.6);
                    glow *= 1.0 + shimmer;
                }

                float sparkle = 0.0;
                if (mode == 1) sparkle = Sparkles(start + along, across, start, start + drawn);

                half alpha = i.color.a;
                half3 rgb = (coreColor * core + glowColor * glow + _SparkleColor.rgb * sparkle) * alpha;
                return half4(rgb, saturate(core + (glow + sparkle) * _GlowOpacity) * alpha);
            }
            ENDHLSL
        }
    }
}
