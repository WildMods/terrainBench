#version 420 core
#extension GL_ARB_shading_language_420pack: require
layout (quads, equal_spacing, ccw) in;
layout (binding = 0) uniform sampler2D heightTex;
uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;
uniform int tilesPerTex;

layout (binding = 3) uniform sampler2D coverageTex;
uniform int mask;
uniform int eyeIdx;
uniform int minDist;

in ivec2 uvOffset[];
in int tileIndex[];

#define WORLD_HEIGHT 800.0

out VertexData {
    vec2 uv;
    vec2 posInTile;
    int shouldCull;
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

int manhattanDist(ivec2 a, ivec2 b) {
    ivec2 d = abs(a - b);
    return d.x + d.y;
}

// Check if a vertex belongs to a low-res tile that should be culled to make
// room for a higher-res one
int cull_by_coverage(int idx, vec2 posInTile) {
    
    int lod = idx >> 16;
    // Figure out this pixel's position within the level 8 tile grid
    int sizeofThisTile = (1 << (8 - lod));
    int index = idx & 0xFFFF;
    index <<= (2 * (8 - lod)); // Convert to index in the level 8 grid
    ivec2 lvl8Pos = (idxToGridPos(index) + ivec2(posInTile.yx));
    
    if (manhattanDist(idxToGridPos(eyeIdx), lvl8Pos) < minDist) {
        return 1; // Cull it
    }

    // Don't draw this part of the tile if a higher-res tile has already been drawn here
    int lodBit = lod - 1;
    int lodCoverage = int(texelFetch(coverageTex, lvl8Pos, 0).r * 255.0);
    // If the value isn't 1 after shifting, that means a higher LOD bit is
    // present (i.e. there is a higher-quality tile available), and/or our LOD
    // bit is unset.
    if (((lodCoverage & mask) >> lodBit) != 1) {
        return 1;
    }
    
    return 0;
}


void main() {
    // get patch coordinate
    int idx = tileIndex[0];
    int lod = idx >> 16;
    int sizeofThisTile = (1 << (8 - lod));
    outData.posInTile = gl_TessCoord.xy * sizeofThisTile;
    
    outData.shouldCull = cull_by_coverage(idx, outData.posInTile);

    ivec2 texelUV = ivec2(gl_TessCoord.yx * 255) + uvOffset[0];
    outData.uv = vec2(texelUV) / (256 * tilesPerTex);
    float height = texelFetch(heightTex, texelUV, 0).x;

    vec3 p00 = gl_in[0].gl_Position.xyz;
    vec3 p01 = gl_in[1].gl_Position.xyz;
    vec3 p10 = gl_in[2].gl_Position.xyz;
    vec3 p11 = gl_in[3].gl_Position.xyz;

    // Interpolate position across patch
    vec3 p0 = (p01 - p00) * gl_TessCoord.x + p00;
    vec3 p1 = (p11 - p10) * gl_TessCoord.x + p10;
    vec3 p = (p1 - p0) * gl_TessCoord.y + p0;

    p.y += height * WORLD_HEIGHT;

    gl_Position = matProjection * matView * matModel * vec4(p, 1);
}
