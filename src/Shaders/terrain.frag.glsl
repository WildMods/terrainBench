#version 420 core
#extension GL_ARB_shading_language_420pack: require
in VertexData {
    float height;
    vec2 uv;
    vec2 posInTile;
}inData;

out vec4 finalColor;
layout (binding = 1) uniform sampler2D matTex;
layout (binding = 2) uniform sampler2DArray colorTextures;

void main() {
    float heightMult = (inData.height / 2) + 0.5;
    vec4 material = texture(matTex, inData.uv);
    ivec2 indices = ivec2(material.xy * 256);
    
    vec3 color1 = texture(colorTextures, vec3(inData.posInTile, indices.x)).rgb;
    vec3 color2 = texture(colorTextures, vec3(inData.posInTile, indices.y)).rgb;
    vec3 matColor = mix(color1, color2, material.z);
    finalColor = vec4(heightMult * matColor, 1);
}
