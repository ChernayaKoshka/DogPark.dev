#!/bin/ash

iptables -P INPUT ACCEPT
iptables -F INPUT

# Cloudflare DNS IP Ranges (should probably set up a script or somethin', but eh)
iptables -A INPUT -p tcp -m multiport --dports http,https -s 173.245.48.0/20 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 103.21.244.0/22 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 103.22.200.0/22 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 103.31.4.0/22 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 141.101.64.0/18 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 108.162.192.0/18 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 190.93.240.0/20 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 188.114.96.0/20 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 197.234.240.0/22 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 198.41.128.0/17 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 162.158.0.0/15 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 104.16.0.0/12 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 172.64.0.0/13 -j ACCEPT
iptables -A INPUT -p tcp -m multiport --dports http,https -s 131.0.72.0/22 -j ACCEPT

# Allow all local traffic
iptables -A INPUT -s 192.168.0.0/24 -j ACCEPT
iptables -A INPUT -s 127.0.0.0/8 -j ACCEPT

IPTABLES -P INPUT DROP