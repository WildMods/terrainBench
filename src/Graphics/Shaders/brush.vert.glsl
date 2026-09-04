#version 330 core
#define M_PI 3.1415926535897932384626433832795

uniform int resolution;
uniform float radius;
uniform mat4 projT;
uniform mat4 viewT;
uniform mat4 modelT;

void main() {
    float angle = (float(gl_VertexID) / float(resolution)) * 2 * M_PI;
    vec4 pos = vec4(cos(angle) * radius, 0, sin(angle) * radius, 1);
    gl_Position = projT * viewT * modelT * pos;
}
