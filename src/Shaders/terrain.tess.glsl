#version 420 core
#extension GL_ARB_shading_language_420pack: require
layout (quads, equal_spacing, ccw) in;
layout (binding = 0) uniform sampler2D heightTex;
uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;
uniform int tilesPerTex;

in ivec2 uvOffset[];
in int tileIndex[];

out VertexData {
    float height; // To be used in fragment shader
    vec2 uv;
    vec2 posInTile;
    flat int tileIdx;
}outData;

void main() {
    // get patch coordinate
    outData.tileIdx = tileIndex[0];
    outData.posInTile = gl_TessCoord.xy;

    ivec2 texelUV = ivec2(gl_TessCoord.yx * 255) + uvOffset[0];
    outData.uv = vec2(texelUV) / (256 * tilesPerTex);
    outData.height = texelFetch(heightTex, texelUV, 0).x;

    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;

    // Interpolate position across patch
    vec4 p0 = (p01 - p00) * gl_TessCoord.x + p00;
    vec4 p1 = (p11 - p10) * gl_TessCoord.x + p10;
    vec4 p = (p1 - p0) * gl_TessCoord.y + p0;

    p.y += outData.height * 16;

    gl_Position = matProjection * matView * matModel * vec4(p.xyz, 1);
}
