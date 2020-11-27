#!/bin/ash
rc-service dogpark stop
cp -af /mnt/media/Private/Staging/* /srv/dogpark/

# Setting proper ownership
chown -R root:dogpark /srv/dogpark/

# Setting all perms to u+rw,g+r, banning anyone else from so much as thinking aobut the dirs/files
chmod -R =o,u+rw,g+r /srv/dogpark/

# Assigning execute perms to all directories for group/user
find /srv/dogpark/ -type d -exec chmod gu+x {} +

# Log perms for dogpark
chmod -R g+rw /srv/dogpark/logs/

# articles/ perms for dogpark
mkdir -p /srv/dogpark/articles/
chmod -R g+rw /srv/dogpark/articles/

rc-service dogpark start