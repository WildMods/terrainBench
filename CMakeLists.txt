cmake_minimum_required(VERSION 3.16)
project(terrainBench VERSION 0.1.0)

# Put all binaries we'll need to distribute in 1 folder that's easy to find
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/bin)

# Force shared libraries to load from a relative path instead of hardcoding
# their path on my computer (why is this the default?)
set(CMAKE_SKIP_BUILD_RPATH FALSE)
set(CMAKE_BUILD_RPATH_USE_ORIGIN TRUE)
set(CMAKE_INSTALL_RPATH "$ORIGIN")
set(CMAKE_INSTALL_RPATH_USE_LINK_PATH FALSE)

add_subdirectory(lib/cs-oead/native)
add_subdirectory(lib/cs-oead/lib/Native.IO/native)
