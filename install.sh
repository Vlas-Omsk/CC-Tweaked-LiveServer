#!/bin/sh

if test -e /usr/local/lib/CCTweaked.LiveServer; then
    sudo rm -r /usr/local/lib/CCTweaked.LiveServer
fi

sudo cp -r ./CCTweaked.LiveServer/bin/Release/net6.0 /usr/local/lib/CCTweaked.LiveServer

if test -e /usr/local/bin/CCTweaked.LiveServer; then
    sudo rm /usr/local/bin/CCTweaked.LiveServer
fi

sudo cp ./start.sh /usr/local/bin/CCTweaked.LiveServer
