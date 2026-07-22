#version 410 core
uniform int[64] indices;
uniform int tilesPerTex;
out vec2 vertUVOffset;
out int tileIdx;

// 1x1 quad vertices
const vec2 base = vec2(0, 0.5);
const vec3 verts[6] = vec3[](
    base.xxx, base.yxx, base.xyx,
    base.yyx, base.yxx, base.xyx
);

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
    int idx = indices[gl_InstanceID];
    if (idx == -1) {
        gl_Position = vec4(0, 0, 0, 1);
        return;
    }
    
    int lod = idx >> 16;
    float tileFactor = float(1 << MAX_LOD) / float(1 << lod);
    ivec2 worldPos = idxToGridPos(idx);

    vec3 pos = verts[gl_VertexID] * tileFactor;
    pos = pos.yzx;
    pos.xz += worldPos * (tileFactor / 2);

    gl_Position = vec4(pos, 1);
    // Bottom 2 bits of the Z-order index tell us where in the 2x2 tile
    // texture to look
    ivec2 posInTile = idxToGridPos(gl_InstanceID);
    vertUVOffset = vec2(posInTile.xy) / tilesPerTex;
    tileIdx = idx;
}
