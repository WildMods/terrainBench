#!/bin/sh
OEAD_BUILD_DIR="lib/cs-oead/native/build"
NATIVEIO_BUILD_DIR="lib/cs-oead/lib/Native.IO/native/build"

mkdir -p $OEAD_BUILD_DIR
cd $OEAD_BUILD_DIR
cmake -G "Ninja" -DCMAKE_BUILD_TYPE=RelWithDebInfo -DCMAKE_POLICY_VERSION_MINIMUM=3.5 ..
ninja

cd ../../../..
mkdir -p $NATIVEIO_BUILD_DIR
cd $NATIVEIO_BUILD_DIR
cmake -G "Ninja" -DCMAKE_BUILD_TYPE=RelWithDebInfo ..
ninja
