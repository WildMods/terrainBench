#version 410 core
// 1x1 quad vertices
const vec2 base = vec2(0, 0.5);
const vec3 verts[6] = vec3[](
    base.xxx, base.yxx, base.xyx,
    base.yyx, base.yxx, base.xyx
);

void main() {
    vec3 pos = verts[gl_VertexID];
    gl_Position = vec4(pos.yzx, 1);
}
