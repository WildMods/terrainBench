#version 420 core
#extension GL_ARB_shading_language_420pack: require
uniform int[4] indices;
in float height;
in vec2 uv;
in vec2 posInTile;
flat in int tileIdx;
out vec4 finalColor;
layout (binding = 1) uniform sampler2D matTex;
layout (binding = 2) uniform sampler2DArray colorTextures;
layout (binding = 3) uniform sampler2D coverageTex;

const int MAX_LOD = 8;

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

void main() {
    int idx = tileIdx;
    int lod = idx >> 16;

    // Figure out this pixel's position within the level 8 tile grid
    int sizeofThisTile = (1 << (8 - lod));
    int index = idx & 0xFFFF;
    index <<= (2 * (8 - lod)); // Convert to index in the level 8 grid
    vec2 lvl8Pos = (idxToGridPos(index) + ivec2(posInTile.yx * sizeofThisTile)) / float(0xFF);

    // Don't draw this part of the tile if a higher-res tile has already been drawn here
    int bestLod = int(texture(coverageTex, lvl8Pos).r * 255.0);
    if (bestLod != lod) {
        discard;
    }

    vec4 material = texture(matTex, uv);
    ivec2 indices = ivec2(material.xy * 256);
    float unknown = material.w;

    vec3 color1 = texture(colorTextures, vec3(uv, indices.x)).rgb;
    vec3 color2 = texture(colorTextures, vec3(uv, indices.y)).rgb;
    vec3 matColor = mix(color1, color2, material.z);
    float heightMult = (height / 2) + 0.5;
    finalColor = vec4(heightMult * matColor, 1);
}
