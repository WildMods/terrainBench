#version 410 core
#extension GL_ARB_shading_language_420pack: require
in float height;
in vec2 uv;
out vec4 finalColor;

void main() {
    // finalColor = vec4(vec2(height) * 0.7 + vec2(uv) * 0.3, 1, 1);
    finalColor = vec4(vec3(height), 1);
}
