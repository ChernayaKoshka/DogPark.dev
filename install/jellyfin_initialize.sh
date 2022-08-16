#!/bin/bash
su -lm jellyfin -c "docker run -d -v /srv/jellyfin/config:/config -v /srv/jellyfin/cache:/cache -v /mnt/media/Media:/media --net=host --name jellyfin jellyfin/jellyfin:latest"
