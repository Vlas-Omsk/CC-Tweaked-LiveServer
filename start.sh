#!/bin/sh

if test -d /usr/local/lib/CCTweaked.LiveServer; then
    INSTALL_DIR=/usr/local/lib/CCTweaked.LiveServer
else
    echo "Could not find CCTweaked.LiveServer installation. Please reinstall."
    exit 1
fi

exec "$INSTALL_DIR/CCTweaked.LiveServer" "$@"
