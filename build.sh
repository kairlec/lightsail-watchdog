#!/bin/sh
set -e

USE_MIRRORS=ustc

# set mirrors from args
if [ $# -gt 0 ]; then
    USE_MIRRORS=$1
fi

docker buildx build --load --no-cache -f Dockerfile --build-arg mirrors=$USE_MIRRORS -t kairlec/lightsail-watchdog . 