#version 410 core
// Tessellation taken mostly from https://learnopengl.com/Guest-Articles/2021/Tessellation/Tessellation

layout (vertices=4) out;
uniform mat4 matModel;
uniform mat4 matView;
uniform mat4 matProjection;

in ivec2 vertUVOffset[];
in int tileIdx[];
out ivec2 uvOffset[];
out int tileIndex[];

#define WORLD_HEIGHT 800.0

// Check if the current patch can be culled
bool canCullPatch() {
    // Get bounding box
    vec3 lo = min(min(gl_in[0].gl_Position.xyz, gl_in[1].gl_Position.xyz),
                  min(gl_in[2].gl_Position.xyz, gl_in[3].gl_Position.xyz));
    vec3 hi = max(max(gl_in[0].gl_Position.xyz, gl_in[1].gl_Position.xyz),
                  max(gl_in[2].gl_Position.xyz, gl_in[3].gl_Position.xyz));
    
    // The min/max height isn't known, so cover the full possible height range.
    lo.y = 0;
    hi.y = WORLD_HEIGHT;

    mat4 mvp = matProjection * matView * matModel;

    // A 6-bit value, each bit representing a clip plane.
    // If any corners fall *inside* a clip plane, that bit will be unset.
    // i.e, if any bit remains set by the end, all 8 corners were outside that
    // plane and the patch can be culled.
    int outside = 0x3F;
    for (int i = 0; i < 8; i++) {
        vec3 corner = vec3((i & 1) != 0 ? hi.x : lo.x,
                           (i & 2) != 0 ? hi.y : lo.y,
                           (i & 4) != 0 ? hi.z : lo.z);
        vec4 c = mvp * vec4(corner, 1);
        int bits = 0;
        bits |= int(c.x < -c.w) << 0;
        bits |= int(c.x >  c.w) << 1;
        bits |= int(c.y < -c.w) << 2;
        bits |= int(c.y >  c.w) << 3;
        bits |= int(c.z < -c.w) << 4;
        bits |= int(c.z >  c.w) << 5;
        outside &= bits;
    }
    return outside != 0;
}

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
        
        if (tileIdx[0] == -1 || canCullPatch()) {
            // A tessellation level of 0 discards the patch
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
