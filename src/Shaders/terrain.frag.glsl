#version 420 core
#extension GL_ARB_shading_language_420pack: require
in float height;
in vec2 uv;
out vec4 finalColor;
layout (binding = 1) uniform sampler2D matTex;

// https://zeldamods.org/wiki/MATE
const vec3 green = vec3(0, 1, 0);
const vec3 darkGreen = vec3(0, 1, 0);
const vec3 grey = vec3(0.5);
const vec3 lightGrey = vec3(0.8);
const vec3 redRock = vec3(0.8, 0, 0);
const vec3 orangeRock = vec3(1, 0.6, 0.25);
const vec3 blueRock = vec3(0.5, 0.5, 0.8);
const vec3 soil = vec3(0.45, 0.35, 0.25);
const vec3 sand = vec3(1, 1, 0.5);
const vec3 white = vec3(1);
const vec3 tanRock = sand;
const vec3 pink = vec3(1, 0.8, 0.8);
const vec3 colors[88] = vec3[](
    green, grey, redRock, grey, lightGrey, soil, sand, sand, white, lightGrey, // 0-9
    sand, soil, grey, grey, green, grey, white, white, white, redRock, // 10 - 19
    lightGrey, orangeRock, grey, redRock, sand, soil, green, grey, sand, // 20 - 29
    grey, grey, green, sand, grey, redRock, redRock, white, soil, sand, // 30 - 39
    green, green, green, green, soil, grey, soil, grey, grey, grey, // 40 - 49
    white, redRock, blueRock, sand, white, white, lightGrey, tanRock, redRock, green, // 50 - 59
    sand, tanRock, tanRock, tanRock, sand, blueRock, white, grey, green, grey, // 60 - 69
    green, redRock, sand, lightGrey, lightGrey, lightGrey, lightGrey, green, green, sand, // 70 - 79
    sand, darkGreen, sand, soil, grey, green, green, pink, pink
);

void main() {
    vec4 material = texture(matTex, uv);
    int idx1 = int(material.x * 255);
    int idx2 = int(material.y * 255);
    float blend = material.z;
    vec3 matColor = colors[idx1] * blend + colors[idx2] * (1 - blend);
    finalColor = vec4(height * matColor, 1);
}
