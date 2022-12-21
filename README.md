# RemoteListenProxy
IPv4 is exhausted, you would be not allocated public external IPv4 anymore. So, you cannot use NAT to open port for cameras, NVR, IOT services, etc.
But you can still connect to any servers. The idea is using server that has public external IPv4 to do socket binding and listening for you. You can you VPS for that purpose. Virtual private servers with sharing public IP are very cheap. Their network speed and bandwidth are high, so it is very good for proxy server.

Data flow model:

[Local Server] <- [Client side adapter] -> ... Internet ... -> [RemoteListenProxy] <- (many clients)

