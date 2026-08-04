#pragma once

// =============================================================================
//  UdpServer.h  —  AsyncUDP tabanlı gerçek zamanlı LED veri alıcı
//
//  Protokol (18 bayt, Mod 3 - Ambilight):
//    Byte  0- 2: LED 0  R, G, B
//    Byte  3- 5: LED 1  R, G, B
//    ...
//    Byte 15-17: LED 5  R, G, B
// =============================================================================

#include <AsyncUDP.h>
#include "LedController.h"

class UdpServer {
public:
    explicit UdpServer(LedController& led);
    void begin();
    void end();

    uint32_t getPacketCount()  const { return _packetCount;  }
    uint32_t getDroppedCount() const { return _droppedCount; }

private:
    AsyncUDP      _udp;
    LedController& _led;
    uint32_t      _packetCount;
    uint32_t      _droppedCount;
};
