#version 420 core
layout (triangles) in;
layout (triangle_strip, max_vertices = 3) out;

in VertexData {
    vec2 uv;
    vec2 posInTile;
    bool shouldCull;
}inData[];

out VertexData {
    vec2 uv;
    vec2 posInTile;
    vec3 normal;
}outData;

/*
    Signed edge function. Returns 0 if the point lies on the edge, -1 if it lies on
    the right side, and 1 if it lies on the left side.
    v0 and v1 are the 2 points forming the edge.
    p is the point to compare with the edge.
*/
float edge_func(vec2 v0, vec2 v1, vec2 p) {
    return determinant(mat2(p - v0, v1 - v0));
}

void main() {
    if (inData[0].shouldCull && inData[1].shouldCull && inData[2].shouldCull) {
        return; // Low-quality triangle, cull it.
    }

    // Use the edge function on the screen coordinates to do backface culling
    vec4 v0 = gl_in[0].gl_Position;
    vec4 v1 = gl_in[1].gl_Position;
    vec4 v2 = gl_in[2].gl_Position;
    if (edge_func(v1.xy / v1.w, v2.xy / v2.w, v0.xy / v0.w) > 0) {
        return; // Face is seen from behind, cull it.
    }
    
    vec3 d1 = v0.xyz - v1.xyz;
    vec3 d2 = v0.xyz - v2.xyz;
    vec3 normal = normalize(cross(d1, d2));

    // Just emit the vertex as-is.
    for (int i = 0; i < 3; i++) {
        gl_Position = gl_in[i].gl_Position;
        outData.uv = inData[i].uv;
        outData.posInTile = inData[i].posInTile;
        outData.normal = normal;
        EmitVertex();
    }    
    EndPrimitive();
}
