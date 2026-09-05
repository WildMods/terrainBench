#version 410 core
uniform int[512] indices;
uniform int tilesPerTex;

uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;

out ivec2 vertUVOffset;
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

vec3 calcVertForIdx(int idx, int vertID, float tileFactor) {
    ivec2 worldPos = idxToGridPos(idx);

    vec3 pos = verts[vertID] + vec3(worldPos.x, 0, worldPos.y);
    pos *= tileFactor;
    return pos;
}

void main() {
    int idx = indices[gl_InstanceID];
    int lod = idx >> 16;
    float tileFactor = float(1 << MAX_LOD) / float(1 << lod);

    bool outsideFrustum = true;
    for (int i = 0; i < 4; i++) {
        vec3 v = calcVertForIdx(idx, i, tileFactor);
        
        vec4 ndcCenter = matView * matModel * vec4(v, 1);
        ndcCenter.y = 0;
        ndcCenter = matProjection * ndcCenter;
        ndcCenter /= ndcCenter.w;
        float threshold = 1;
        if (abs(ndcCenter.x) > threshold || abs(ndcCenter.y) > threshold || abs(ndcCenter.z) > threshold) {
        } else {
            outsideFrustum = false;
        }
    }

    if (outsideFrustum) {
        tileIdx = -1;
        gl_Position = vec4(0, 0, 2, 1);
        return;
    }
    
    vec3 pos = calcVertForIdx(idx, gl_VertexID, tileFactor);

    gl_Position = vec4(pos, 1);
    ivec2 gridPos = idxToGridPos(gl_InstanceID);
    vertUVOffset = ivec2(gridPos.xy) * 256;
    tileIdx = idx;
}
