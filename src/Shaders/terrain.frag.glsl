#version 420 core
#extension GL_ARB_shading_language_420pack: require
in float height;
in vec2 uv;
out vec4 finalColor;
layout (binding = 1) uniform sampler2D matTex;
layout (binding = 2) uniform sampler2DArray colorTextures;

void main() {
    vec4 material = texture(matTex, uv);
    int idx1 = int(material.x * 255);
    int idx2 = int(material.y * 255);
    float blend = material.z;

    vec3 color1 = texture(colorTextures, vec3(uv, idx1)).rgb;
    vec3 color2 = texture(colorTextures, vec3(uv, idx2)).rgb;
    vec3 matColor = color1 * blend + color2 * (1 - blend);
    float heightMult = (height / 2) + 0.5;
    finalColor = vec4(heightMult * matColor, 1);
}
