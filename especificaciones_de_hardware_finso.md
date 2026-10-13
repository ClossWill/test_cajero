# Guía de Integración y Arquitectura del "Agente ATM" (Especificaciones de Hardware)
**Referencia:** HZ20260125
**Proyecto:** Agente ATM FINSO (C# .NET)

Este documento contiene la transcripción técnica de las partidas de hardware de la máquina industrial, detallando los modelos y el dictamen técnico para su integración mediante código.

---

## 1. Componentes Core que Requieren Programación Directa (Librerías / Protocolos)
Estos componentes representan el núcleo transaccional del cajero y deben ser probados mediante métodos directos.

| Part. | Componente / Modelo | Especificación de Hardware | Integración y Desarrollo |
| :--- | :--- | :--- | :--- |
| **5** | **Cash recycler with shutter (GRG 9250N)** | Recicladora de 4 casetes de aceptación (2,850 notas c/u) + Shutter. | **Inyección de código fuente:** Se debe utilizar el archivo nativo abierto `CRM9250N.cs` en la solución .NET. Contiene los métodos de comunicación. |
| **7** | **Coin dispenser (MK4 - 2 PCS)** | Dispensador doble de monedas multimoneda. 3 monedas/seg. | **Protocolo serial (ccTalk):** Uso de librería genérica ccTalk de código abierto en C#. Se envían bytes de comandos específicos por puerto COM. |
| **8** | **Passport scanner (MEPR500+)** | Sensor CMOS 3.1 MP, 400 DPI. Lectura RFID e Infrarrojo (ICAO 9303). | **Consumo de SDK Propietario:** Invocación de funciones OCR en segundo plano para extraer Nombre, ID y Nacionalidad. |
| **9** | **Receipt Printer (KP-80A)** | Impresora térmica de 80mm con autocortador. Velocidad 250mm/s. | **Comandos Estándar ESC/POS:** Conectada vía USB/Serial. Envío de strings/comandos genéricos de impresión industrial para el ticket. |

---

## 2. Sensores, Seguridad y Periféricos con Comunicación Automatizada
Componentes que reportan eventos o son autónomos.

| Part. | Componente / Modelo | Especificación de Hardware | Integración y Desarrollo |
| :--- | :--- | :--- | :--- |
| **16** | **Multifunctional serial control module (ALB02)** | Concentrador de sensores internos (switch de puertas, vibración/anti-vandalismo, proximidad). | **Escucha Serial Reactiva:** Envía cadenas de bytes automáticas por puerto COM. Se debe leer el puerto y reaccionar al cambio de estado. |
| **10** | **QR code scanner (XZG004)** | Escáner 1D/2D, CMOS 1280x800 píxeles. | **Plug & Play (Cero Programación):** Emula teclado (HID-KBW). Teclea directamente en el foco de la pantalla. |
| **11, 12** | **Camera+NVR (Hikvision) & Camera (GDZS-DC004)** | NVR DS-7604NI-Q1 (1TB) + Cámara USB nativa (Luz visible 300W/IR 200W). | **Dispositivo Estándar:** La cámara USB la detecta el SO. Se puede usar OpenCV o librerías multimedia de .NET. NVR es autónomo. |

---

## 3. Infraestructura, Periféricos Eléctricos y Sistema Operativo Nativo
Base de cómputo y energía (Sin programación requerida en la capa lógica).

| Part. | Componente / Modelo | Especificación de Hardware | Detalles Adicionales |
| :--- | :--- | :--- | :--- |
| **1** | **Industrial PC System** | Motherboard H310P12C, Intel i5-8500, 8GB RAM, 256GB SSD. | Entorno local. Cuenta con puertos USB, COM (Serial) y LAN. |
| **2** | **Operation System** | Windows 11 IoT Enterprise LTSC 2024. | Provee kernel nativo y asignación de puertos (COM/USB). |
| **3, 4** | **Display & Touch screens** | 19" Touch Capacitiva (1280x1024) + Pantalla superior 21.5". | Salidas HDMI/VGA. Touch via protocolo USB HID. |
| **13, 14, 17** | **Conectividad / Clima** | TP-Link Switch, Router 3G/4G, Controlador temperatura. | Manejo autónomo físico. |
| **6, 19-21** | **Gabinete y Energía** | Chasis CRS 2.5mm, Bóveda 8mm. Fuentes Mean Well. Cerradura S&G. | Proveen el voltaje requerido para actuadores (GRG, MK4). |