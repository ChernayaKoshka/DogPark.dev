#! /bin/ash
rc-service dogpark stop
cp -af /mnt/media/Private/Staging/* /srv/dogpark/

# Setting proper ownership
chown -R root:dogpark /srv/dogpark/

# Setting all perms to u+rw,g+r
chmod -R =u+rw,g+r /srv/dogpark/

# Assigning execute perms to all directories for group/user
find /srv/dogpark/ -type d -exec chmod gu+x {} +

# Log perms for dogpark
chmod -R g+rw /srv/dogpark/Logs/

# DogPark-Articles/ perms for dogpark
chmod -R g+rw /srv/dogpark/DogPark-Articles/

rc-service dogpark start