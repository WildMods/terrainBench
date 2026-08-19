#version 430
layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;
uniform layout(binding=0,rgba8ui) readonly uimage2DArray baseArray;
uniform layout(binding=1,rgba8ui) writeonly uimage2DArray mipArray;

void computeMipMap() {
    ivec3 globalID = ivec3(gl_GlobalInvocationID);
    ivec3 posInBase = ivec3(2 * globalID.x, 2 * globalID.y, globalID.z);
    ivec3 offset = ivec3(0, 1, 0);

    uvec4 color = (
        imageLoad(baseArray, posInBase + offset.xxz) +
        imageLoad(baseArray, posInBase + offset.xyz) +
        imageLoad(baseArray, posInBase + offset.yxz) +
        imageLoad(baseArray, posInBase + offset.yyz)
    ) / 4;
    imageStore(mipArray, globalID.xyz, color);
}

void main() {
    computeMipMap();
    return;
}