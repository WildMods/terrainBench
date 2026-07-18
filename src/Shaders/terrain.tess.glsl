#version 420 core
#extension GL_ARB_shading_language_420pack: require
layout (quads, equal_spacing, ccw) in;
layout (binding = 0) uniform sampler2D heightTex;
uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;

in vec2 uvOffset[];
out float height; // To be used in fragment shader
out vec2 uv;

void main() {
    // get patch coordinate
    float u = gl_TessCoord.y / 2;
    float v = gl_TessCoord.x / 2;
    // Bottom 2 bits of the Z-order index tell us where in the 2x2 tile
    // texture to look
    u += uvOffset[0].x;
    v += uvOffset[0].y;

    uv = vec2(u, v);
    height = texture(heightTex, uv).x;

    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;
    
    // Interpolate position across patch
    vec4 p0 = (p01 - p00) * gl_TessCoord.x + p00;
    vec4 p1 = (p11 - p10) * gl_TessCoord.x + p10;
    vec4 p = (p1 - p0) * gl_TessCoord.y + p0;

    p.y += height * 16;

    gl_Position = matProjection * matView * matModel * vec4(p.xyz, 1);
}
