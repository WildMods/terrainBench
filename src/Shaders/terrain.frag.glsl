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
    int index = idx & 0xFFFF;
    int sizeofThisTile = (1 << (8 - lod));
    int lvl8Mult = 1 << (2 * (8 - lod)); // Convert to index in the level 8 grid
    index *= lvl8Mult;

    float tileFactor = float(1 << MAX_LOD) / float(1 << lod);
    vec2 lvl8Pos = (idxToGridPos(index) + ivec2(+1, +1) * ivec2(posInTile.yx * sizeofThisTile)) / float(0xFF);

    int bestLod = int(texture(coverageTex, lvl8Pos).r * 255.0) >> 4;
    if (bestLod != lod) {
        discard;
    }

    vec4 material = texture(matTex, uv);
    int idx1 = int(material.x * 255);
    int idx2 = int(material.y * 255);
    float blend = material.z;
    float unknown = material.w;

    vec3 color1 = texture(colorTextures, vec3(uv, idx1)).rgb;
    vec3 color2 = texture(colorTextures, vec3(uv, idx2)).rgb;
    vec3 matColor = color1 * (1.0 - blend) + color2 * (blend);
    float heightMult = (height / 2) + 0.5;
    finalColor = vec4(heightMult * matColor, 1);
}
