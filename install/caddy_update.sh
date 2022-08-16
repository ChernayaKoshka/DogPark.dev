#!/bin/bash
rc-service caddy stop
cd /tmp
curl -o caddy "https://caddyserver.com/api/download?os=linux&arch=amd64&p=github.com%2Fcaddy-dns%2Fcloudflare"
chmod +x ./caddy
mv ./caddy /usr/sbin/caddy
setcap CAP_NET_BIND_SERVICE=+eip /usr/sbin/caddy
/usr/sbin/caddy list-modules
rc-service caddy restart