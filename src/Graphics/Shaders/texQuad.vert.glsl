#version 330 core

// 1x1 quad vertices
const vec2 base = vec2(0, 1.0);
const vec2 uvs[6] = vec2[](
    base.xx, base.xy, base.yx,
    base.yy, base.xy, base.yx
);

uniform vec4 rect; // X/Y = min point, Z/W = max point
out vec2 uv;

void main() {
    vec2 verts[6] = vec2[](
        rect.xy, rect.xw, rect.zy, // min-min, min-max, max-min
        rect.zw, rect.xw, rect.zy  // max-max, min-max, max-min
    );
    
    gl_Position = vec4(verts[gl_VertexID], 0, 1.0);
    uv = uvs[gl_VertexID];
}