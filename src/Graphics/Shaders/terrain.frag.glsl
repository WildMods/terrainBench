#version 420 core
#extension GL_ARB_shading_language_420pack: require
in VertexData {
    vec2 uv;
    vec2 posInTile;
    vec3 normal;
}inData;

out vec4 finalColor;
layout (binding = 1) uniform sampler2D matTex;
layout (binding = 2) uniform sampler2DArray colorTextures;

void main() {
    vec3 lightdir = vec3(0, 1, 0);
    float diffuseMult =  (1.0 - dot(lightdir, inData.normal));
    vec4 material = texture(matTex, inData.uv);
    ivec2 indices = ivec2(material.xy * 256);
    
    vec3 color1 = texture(colorTextures, vec3(inData.posInTile, indices.x)).rgb;
    vec3 color2 = texture(colorTextures, vec3(inData.posInTile, indices.y)).rgb;
    vec3 matColor = mix(color1, color2, material.z);
    finalColor = vec4(diffuseMult * matColor, 1);
}
