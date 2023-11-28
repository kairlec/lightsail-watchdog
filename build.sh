#!/bin/sh
set -e

docker buildx build --load --no-cache -f Dockerfile -t kairlec/lightsail-watchdog .