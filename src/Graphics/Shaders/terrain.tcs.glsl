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
        // Set tessellation power to texel count, so that there's 1 more vertex
        // than the pixel area the patch is trying to cover. This extra vertex
        // is the shared edge with the neighboring patch.
        // The rounding behaviour makes this works out to be 63 instead of 64 on
        // 1 axis on the 0 edge, which causes the total tesellation power to add
        // up to 255 on both axes (producing a total grid of 256x256 vertices).
        int levelX = int(vertUVOffset[2].x) - int(vertUVOffset[0].x); // Along v (gl_TessCoord.y)
        int levelZ = int(vertUVOffset[1].y) - int(vertUVOffset[0].y); // Along u (gl_TessCoord.x)
        
        if (tileIdx[0] == -1) {
            levelX = 0;
            levelZ = 0;
        }

        gl_TessLevelOuter[0] = levelX; // u = 0 edge, runs along v
        gl_TessLevelOuter[1] = levelZ; // v = 0 edge, runs along u
        gl_TessLevelOuter[2] = levelX; // u = 1 edge
        gl_TessLevelOuter[3] = levelZ; // v = 1 edge

        gl_TessLevelInner[0] = levelZ; // Along u
        gl_TessLevelInner[1] = levelX; // Along v
    }
}
