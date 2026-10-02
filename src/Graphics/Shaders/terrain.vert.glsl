#version 410 core
layout (location = 0) in vec2 patchPos; // 2D patch vertices within a tile
uniform int[512] indices;
uniform int tilesPerTex;

uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;

out ivec2 vertUVOffset; // Location of the vertex in the atlas
out int tileIdx;

// 1x1 quad vertices
const vec2 base = vec2(0, 1.0);
const vec3 verts[6] = vec3[](
    base.xxx, base.xxy, base.yxx,
    base.yxy, base.xxy, base.yxx
);

const uint MAX_LOD = 8;

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

// @param idx Z-order index of the tile
// @param posInTile Position of the vertex within the tile (i.e. units of tiles from 0 - 1)
// @param tileFactor Final scale factor (usually for scaling by LOD level)
vec3 calcVertForIdx(int idx, vec3 posInTile, float tileFactor) {
    ivec2 worldPos = idxToGridPos(idx);

    vec3 pos = posInTile + vec3(worldPos.x, 0, worldPos.y);
    pos *= tileFactor;
    return pos;
}

void main() {
    int idx = indices[gl_InstanceID];
    int lod = idx >> 16;
    float tileFactor = float(1 << MAX_LOD) / float(1 << lod);

    // Figure out if this entire tile will be outside the frustum and can be culled
    bool outsideFrustum = true;
    for (int i = 0; i < 4 * 4; i++) {
        vec3 v = calcVertForIdx(idx, verts[i], tileFactor);
        
        vec4 ndcCenter = matView * matModel * vec4(v, 1);
        // Ignore the Y component of the camera, to essentially do 2D clipping against the tile grid.
        ndcCenter.y = 0;
        ndcCenter = matProjection * ndcCenter;
        ndcCenter /= ndcCenter.w;
        
        float threshold = 1;
        if (abs(ndcCenter.x) < threshold && abs(ndcCenter.y) < threshold && abs(ndcCenter.z) < threshold) {
            outsideFrustum = false;
            break;
        }
    }

    if (outsideFrustum) {
        tileIdx = -1;
        vertUVOffset = ivec2(0);
        gl_Position = vec4(0, 0, 2, 1);
        return; // Cull the whole tile.
    }
    
    // Multiply by 255 since that's the last pixel position in a tile
    ivec2 texelInTile = ivec2(patchPos * 255);
    vec3 pos = calcVertForIdx(idx, vec3(patchPos.x, 0, patchPos.y), tileFactor);

    gl_Position = vec4(pos, 1);
    ivec2 gridPos = idxToGridPos(gl_InstanceID);
    vertUVOffset = gridPos * 256 + texelInTile;
    tileIdx = idx;
}
