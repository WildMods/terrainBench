#version 410 core
// Tessellation taken mostly from https://learnopengl.com/Guest-Articles/2021/Tessellation/Tessellation

layout (vertices=4) out;
in ivec2 vertUVOffset[];
in int tileIdx[];
out ivec2 uvOffset[];
out int tileIndex[];

void main() {
    vec4 pos = gl_in[gl_InvocationID].gl_Position;
    gl_out[gl_InvocationID].gl_Position = pos;
    uvOffset[gl_InvocationID] = vertUVOffset[gl_InvocationID];
    tileIndex[gl_InvocationID] = tileIdx[gl_InvocationID];
    
    // Invocation 0 controls tessellation levels for the entire patch
    if (gl_InvocationID == 0) {
        // Tessellate to 256 vertices square
        int tessLevel = 255;
        if (tileIndex[gl_InvocationID] == -1) {
            tessLevel = 0;
        }
        
        gl_TessLevelOuter[0] = tessLevel;
        gl_TessLevelOuter[1] = tessLevel;
        gl_TessLevelOuter[2] = tessLevel;
        gl_TessLevelOuter[3] = tessLevel;

        gl_TessLevelInner[0] = tessLevel;
        gl_TessLevelInner[1] = tessLevel;
    }
}
