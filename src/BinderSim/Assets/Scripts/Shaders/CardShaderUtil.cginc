#ifndef CARDUTIL
#define CARDUTIL

inline float4 Blend( float4 a, float4 b, float t )
{
    float4 r = a;
    r.a = b.a + a.a * ( 1 - b.a );
    r.rgb = ( b.rgb * b.a + a.rgb * a.a * ( 1 - b.a ) ) * ( r.a + 0.0000001 );
    r.a = saturate( r.a );
    return lerp( a, r, t );
}

inline float4 BlendPreferHighlight( float4 a, float4 b, float t )
{
    float4 r = a;
    r.a = b.a + a.a * ( 1 - b.a );
    r.rgb = pow( ( b.rgb * b.a + a.rgb * a.a * ( 1 - b.a ) ) * ( r.a + 0.0000001 ), 1.0 + ( a.r + a.g + a.b ) * 1.0 );
    r.a = saturate( r.a );
    return lerp( a, r, t );
}

inline float4 Parallax( float2 uv, sampler2D source, float intensity )
{
    float x = ( -unity_ObjectToWorld[0][2] * 0.1 );
    float4 rgb = tex2D( source, uv + float2( x, 0 ) );
    float r = tex2D( source, uv + float2( x * ( 1 - intensity ), 0 ) ).r;
    float b = tex2D( source, uv + float2( x * ( 1 + intensity ), 0 ) ).b;
    return float4( r, rgb.g, b, rgb.a );
}

inline float4 Foil( float4 source, float2 uv, float speed, float t )
{
    float a = 2.0 * uv.x;
    float b = sin( ( _Time.y * speed * 2.2 ) + 1.1 + a ) + sin( ( _Time.y * speed * 1.8 ) + 0.5 - a ) + sin( ( _Time.y * speed * 1.5 ) + 8.2 + 2.0 * uv.y ) + sin( ( _Time.y * speed * 2.0 ) + 595 + 5.0 * uv.y );
    float c = ( ( 5.0 + b ) / 5.0 ) - floor( ( ( 5.0 + b ) / 5.0 ) );
    float d = c + source.r * 0.2 + source.g * 0.4 + source.b * 0.2;
    d = ( d - floor( d ) ) * 8;
    return lerp( source, float4( clamp( d - 4.0, 0.0, 1.0 ) + clamp( 2.0 - d, 0.0, 1.0 ), d < 2.0 ? clamp( d, 0.0, 1.0 ) : clamp( 4.0 - d, 0.0, 1.0 ), d < 4.0 ? clamp( d - 2.0, 0.0, 1.0 ) : clamp( 6.0 - d, 0.0, 1.0 ), source.a ), t );
}

inline float4 Shine( float2 uv, float position, float size, float smoothing, float intensity )
{
    uv = uv - float2( position + 0.5, 0.5 );
    float a = atan2( uv.x, uv.y ) + 1.4;
    float r = 3.1415;
    float c = cos( floor( 0.5 + a / r ) * r - a ) * length( uv );
    float d = 1.0 - smoothstep( size, size + smoothing, c );
    return d * intensity;
}

// Based on GPU Gems
// Optimised by Alan Zucconi
inline float3 bump3y( float3 x, float3 yoffset )
{
    float3 y = float3( 1., 1., 1. ) - x * x;
    y = saturate( y - yoffset );
    return y;
}

inline float3 spectral_zucconi6( float x )
{
    float3 c1 = float3( 3.54585104, 2.93225262, 2.41593945 );
    float3 x1 = float3( 0.69549072, 0.49228336, 0.27699880 );
    float3 y1 = float3( 0.02312639, 0.15225084, 0.52607955 );

    float3 c2 = float3( 3.90307140, 3.21182957, 3.96587128 );
    float3 x2 = float3( 0.11748627, 0.86755042, 0.66077860 );
    float3 y2 = float3( 0.84897130, 0.88445281, 0.73949448 );

    return bump3y( c1 * ( x - x1 ), y1 ) + bump3y( c2 * ( x - x2 ), y2 );
}

inline float2 hash( float2 p )
{
    p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
    return -1.0 + 2.0 * frac( sin( p ) * 43758.5453123 );
}

inline float noise( float2 p )
{
    const float K1 = 0.366025404; // (sqrt(3)-1)/2;
    const float K2 = 0.211324865; // (3-sqrt(3))/6;

    float2  i = floor( p + ( p.x + p.y ) * K1 );
    float2  a = p - i + ( i.x + i.y ) * K2;
    float m = step( a.y, a.x );
    float2  o = float2( m, 1.0 - m );
    float2  b = a - o + K2;
    float2  c = a - 1.0 + 2.0 * K2;
    float3  h = max( 0.5 - float3( dot( a, a ), dot( b, b ), dot( c, c ) ), 0.0 );
    float3  n = h * h * h * h * float3( dot( a, hash( i + 0.0 ) ), dot( b, hash( i + o ) ), dot( c, hash( i + 1.0 ) ) );
    return dot( n, float3( 70.0, 70.0, 70.0 ) );
}

inline float2x2 Rot( float a ) 
{
    float c = cos( a ), s = sin( a );
    return float2x2( c, -s, s, c );
}

inline float Star( float2 uv, float flare ) 
{
    float col = 0.;
    float d = length( uv );
    float m = .02 / d;

    float rays = max( 0., 1. - abs( uv.x * uv.y * 1000. ) );
    m += rays * flare;
    uv = mul(uv, Rot( 3.1415 / 4. ) );
    rays = max( 0., 1. - abs( uv.x * uv.y * 1000. ) );
    m += rays * .3 * flare;

    m *= smoothstep( 1., .2, d );

    return m;
}

#endif