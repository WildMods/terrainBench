#version 410 core
#extension GL_ARB_shading_language_420pack: require
layout (quads, equal_spacing, ccw) in;
layout (binding = 0) uniform sampler2D tex;
uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;
uniform int idx;

out float height; // To be used in fragment shader
out vec2 uv;

const float targetTileSize = 8;
const int MAX_LOD = 8;

// De-interleave the low 16 bits to get an 8-bit X/Z coordinate
ivec2 idxToGridPos(int idx) {
    ivec2 pos = ivec2(0);
    for (int i = 0; i < 16; i+= 2) {
        pos.y <<= 1;
        pos.y |= ((idx >> 15) & 1);
        idx <<= 1;

        pos.x <<= 1;
        pos.x |= ((idx >> 15) & 1);
        idx <<= 1;
    }
    return pos;
}

void main() {
    // get patch coordinate
    float u = gl_TessCoord.y / 2;
    float v = gl_TessCoord.x / 2;
    // Bottom 2 bits of the Z-order index tell us where in the 2x2 tile
    // texture to look
    u += (0.5 * float(idx & 1));
    v += (0.5 * float((idx >> 1) & 1));

    uv = vec2(u, v);
    height = texture(tex, uv).x;

    int lod = idx >> 16;
    float tileFactor = float(1 << MAX_LOD) / float(1 << lod);
    ivec2 worldPos = idxToGridPos(idx);

    vec4 p00 = gl_in[0].gl_Position * tileFactor;
    vec4 p01 = gl_in[1].gl_Position * tileFactor;
    vec4 p10 = gl_in[2].gl_Position * tileFactor;
    vec4 p11 = gl_in[3].gl_Position * tileFactor;
    
    // Interpolate position across patch
    vec4 p0 = (p01 - p00) * gl_TessCoord.x + p00;
    vec4 p1 = (p11 - p10) * gl_TessCoord.x + p10;
    vec4 p = (p1 - p0) * gl_TessCoord.y + p0;

    p.y += height * 16;
    p.xz += worldPos * (tileFactor / 2);

    gl_Position = matProjection * matView * matModel * vec4(p.xyz, 1);
}
