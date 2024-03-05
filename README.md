# totalip-socket-listener
Escuta eventos na porta socket do servidor de aplicação da TotalIp e publica eventos em API REST designada em configuração.

```bash
sudo docker run --name totalip-socket-listener \
                -e Serilog__MinimumLevel__Default=Debug \
                -e TotalIp__SocketServerHost=192.168.0.10 \
                -e TotalIp__SocketServerPort=25000 \
                -e TotalIp__PublishingApiKey=5ceebd7f0ee24a49a0ed78d1e9b94ccc \
                -e TotalIp__PublishingApiBaseAddress=http://api.totalip.mydomain.local \
                --restart=always
                --memory=512M
                -d ghcr.io/banqercrm/totalip-socket-listener:latest
```
