#!/bin/sh
BUILD_DIR="lib/cs-oead/native/build"

mkdir $BUILD_DIR
cd $BUILD_DIR
cmake -G "Ninja" -DCMAKE_BUILD_TYPE=Release -DCMAKE_POLICY_VERSION_MINIMUM=3.5 ..
ninja
