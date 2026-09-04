#version 420 core
layout (triangles) in;
layout (triangle_strip, max_vertices = 3) out;
layout (binding = 3) uniform sampler2D coverageTex;

in VertexData {
    float height;
    vec2 uv;
    vec2 posInTile;
    flat int tileIdx;
}inData[];

out VertexData {
    float height;
    vec2 uv;
    vec2 posInTile;
    vec3 normal;
}outData;

// Copied from vertex shader
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


/*
    Signed edge function. Returns 0 if the point lies on the edge, -1 if it lies on
    the right side, and 1 if it lies on the left side.
    v0 and v1 are the 2 points forming the edge.
    p is the point to compare with the edge.
*/
float edge_func(vec2 v0, vec2 v1, vec2 p) {
    return determinant(mat2(p - v0, v1 - v0));
}

// Check if a vertex belongs to a low-res tile that should be culled to make
// room for a higher-res one
bool cull_by_coverage(int i) {
    int idx = inData[i].tileIdx;
    int lod = idx >> 16;
    // Figure out this pixel's position within the level 8 tile grid
    int sizeofThisTile = (1 << (8 - lod));
    int index = idx & 0xFFFF;
    index <<= (2 * (8 - lod)); // Convert to index in the level 8 grid
    ivec2 lvl8Pos = (idxToGridPos(index) + ivec2(inData[i].posInTile.yx));

    // Don't draw this part of the tile if a higher-res tile has already been drawn here
    int lodBit = lod - 1;
    int lodCoverage = int(texelFetch(coverageTex, lvl8Pos, 0).r * 255.0);
    // If the value isn't 1 after shifting, that means a higher LOD bit is
    // present (i.e. there is a higher-quality tile available), and/or our LOD
    // bit is unset.
    if ((lodCoverage >> lodBit) != 1) {
        return true;
    }
    
    return false;
}

void main() {
    if (cull_by_coverage(0) && cull_by_coverage(1) && cull_by_coverage(2)) {
        return; // Low-quality triangle, cull it.
    }

    // Use the edge function on the screen coordinates to do backface culling
    vec4 v0 = gl_in[0].gl_Position;
    vec4 v1 = gl_in[1].gl_Position;
    vec4 v2 = gl_in[2].gl_Position;
    if (edge_func(v1.xy / v1.w, v2.xy / v2.w, v0.xy / v0.w) > 0) {
        return; // Face is seen from behind, cull it.
    }
    
    vec3 d1 = v0.xyz - v1.xyz;
    vec3 d2 = v0.xyz - v2.xyz;
    vec3 normal = normalize(cross(d1, d2));

    // Just emit the vertex as-is.
    for (int i = 0; i < 3; i++) {
        gl_Position = gl_in[i].gl_Position;
        outData.height = inData[i].height;
        outData.uv = inData[i].uv;
        outData.posInTile = inData[i].posInTile;
        outData.normal = normal;
        EmitVertex();
    }    
    EndPrimitive();
}
