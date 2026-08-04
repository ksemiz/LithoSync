// =============================================================================
//  UdpServer.cpp  —  AsyncUDP Ambilight veri alıcısı + Otomatik Keşif
// =============================================================================

#include "UdpServer.h"
#include "Config.h"
#include <Arduino.h>
#include <WiFi.h>

UdpServer::UdpServer(LedController& led)
    : _led(led), _packetCount(0), _droppedCount(0)
{}

void UdpServer::begin() {
    bool ok = _udp.listen(UDP_PORT);
    if (!ok) {
        Serial.printf("[UDP] Port %d dinlenemedi!\n", UDP_PORT);
        return;
    }

    Serial.printf("[UDP] Port %d dinleniyor (Ambilight + Discovery).\n", UDP_PORT);

    _udp.onPacket([this](AsyncUDPPacket& packet) {
        size_t len = packet.length();

        // ── Otomatik Cihaz Keşfi (Discovery) ──────────────────────────────
        if (len >= 17 && memcmp(packet.data(), "LITHOSYNC_DISCOVER", 17) == 0) {
            String ipStr = WiFi.localIP().toString();
            packet.printf("{\"device\":\"LithoSync\",\"ip\":\"%s\",\"ver\":\"1.0.0\"}", ipStr.c_str());
            Serial.printf("[UDP] Discovery isteği yanıtlandı: %s\n", ipStr.c_str());
            return;
        }

        // Yalnızca Mod 3'te Ambilight verisini işle
        if (_led.getMode() != MODE_UDP) {
            _droppedCount++;
            return;
        }

        if (len < (size_t)(NUM_LEDS * 3)) {
            _droppedCount++;
            return;
        }

        _packetCount++;
        _led.writeUdpData(packet.data(), len);
    });
}

void UdpServer::end() {
    _udp.close();
    Serial.println("[UDP] Sunucu kapatıldı.");
}
