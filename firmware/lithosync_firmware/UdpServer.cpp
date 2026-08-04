// =============================================================================
//  UdpServer.cpp  —  AsyncUDP Ambilight veri alıcısı
// =============================================================================

#include "UdpServer.h"
#include "Config.h"
#include <Arduino.h>

UdpServer::UdpServer(LedController& led)
    : _led(led), _packetCount(0), _droppedCount(0)
{}

void UdpServer::begin() {
    bool ok = _udp.listen(UDP_PORT);
    if (!ok) {
        Serial.printf("[UDP] Port %d dinlenemedi!\n", UDP_PORT);
        return;
    }

    Serial.printf("[UDP] Port %d dinleniyor (Ambilight protokolü).\n", UDP_PORT);
    Serial.printf("[UDP] Beklenen paket boyutu: %d bayt (%d LED × 3)\n",
                  NUM_LEDS * 3, NUM_LEDS);

    // Paket geldğinde callback — AsyncUDP bu callback'i ağ görevinde çağırır
    _udp.onPacket([this](AsyncUDPPacket& packet) {
        size_t len = packet.length();

        // Yalnızca Mod 3'te işle
        if (_led.getMode() != MODE_UDP) {
            _droppedCount++;
            return;
        }

        // Minimum beklenen boyut: NUM_LEDS * 3
        if (len < (size_t)(NUM_LEDS * 3)) {
            _droppedCount++;
            Serial.printf("[UDP] Geçersiz paket boyutu: %d (beklenen: %d)\n",
                          (int)len, NUM_LEDS * 3);
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
