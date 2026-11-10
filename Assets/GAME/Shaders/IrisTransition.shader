// Transition « iris » : écran couvert sauf un cercle au centre, dont le rayon s'anime.
// _Radius : 0 = fermé (tout couvert), 1 = ouvert (le cercle dépasse les coins de l'écran).
// Le bord est découpé en gros pixels (_PixelSize) pour coller au pixel art, avec un liseré coloré.
Shader "BrotatoLike/UI/IrisTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Radius ("Radius (0 = closed, 1 = open)", Range(0, 1)) = 1
        _Center ("Center (0-1 on screen)", Vector) = (0.5, 0.5, 0, 0)
        _PixelSize ("Pixel size (screen pixels)", Float) = 6
        _EdgeColor ("Edge color", Color) = (1, 0.75, 0.25, 1)
        _EdgeWidth ("Edge width (screen pixels)", Float) = 12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Radius;
            float4 _Center;
            float _PixelSize;
            float _EdgeWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Position du pixel à l'écran, arrondie à la « grille » de gros pixels
                float2 px = i.pos.xy;
                #if UNITY_UV_STARTS_AT_TOP
                px.y = _ScreenParams.y - px.y;
                #endif
                float size = max(1.0, _PixelSize);
                px = (floor(px / size) + 0.5) * size;

                // Rayon ouvert = la diagonale de l'écran : couvre tout, quel que soit le centre
                float2 center = _Center.xy * _ScreenParams.xy;
                float radius = _Radius * length(_ScreenParams.xy);
                float d = distance(px, center);

                if (d < radius) return fixed4(0, 0, 0, 0);
                if (d < radius + _EdgeWidth && _Radius > 0.001) return _EdgeColor * fixed4(1, 1, 1, i.color.a);
                return i.color;
            }
            ENDCG
        }
    }
}
