Contexto del Proyecto: Pruebas de Comunicación Hardware Agente ATM (FINSO)

⚠️ DIRECTRIZ PRINCIPAL (CRÍTICA)

NO ES UNA APLICACIÓN, NI UN MIDDLEWARE, NI UN PUENTE.
El objetivo exclusivo de este proyecto es generar un código de pruebas (Test Harness / PoC) en una simple Aplicación de Consola en C# .NET 8.

El propósito es puramente técnico y exploratorio: asegurar que podemos comunicarnos físicamente con cada periférico de la máquina. Todo se ejecutará mediante llamadas directas a métodos desde la función Main (ej. EntregarDinero(), LeerPasaporte(), ImprimirTicket()).

Una vez validada la comunicación y el comportamiento del hardware con este código de prueba, estos métodos serán extraídos y llevados a la aplicación oficial en el futuro.

🛠 Entorno de Desarrollo

Lenguaje: C#

Framework: .NET 8

Tipo de Proyecto: Aplicación de Consola (Console App)

Sistema Operativo Objetivo: Windows 11 IoT Enterprise (Local en la máquina)

💻 Hardware a Integrar y Métodos de Prueba a Desarrollar

Basado en la "Guía de Integración_Hardware BEEX.pdf" y el diagrama de flujo de cambio de divisas, se deben programar los siguientes métodos estáticos o clases simples para ser llamados desde el Main:

1. Recicladora de Efectivo (GRG 9250N)

Integración: Uso del archivo fuente nativo CRM9250N.cs.

Métodos de Prueba Requeridos:

Test_InicializarRecicladora(): Levantar el servicio y comprobar conexión.

Test_AceptarBilletes(): Abrir shutter, meter un billete físico, leer su denominación/moneda y mandarlo al escrow.

Test_DispensarBilletes(int cantidad, string moneda): Sacar billetes de prueba de los casetes y entregarlos por el shutter.

2. Dispensadores de Monedas (MK4 - 2 unidades)

Integración: Protocolo Serial ccTalk.

Métodos de Prueba Requeridos:

Test_PingMonederos(): Validar comunicación serial en el puerto COM.

Test_DispensarMonedas(int cantidad_1_MXN, int cantidad_5_MXN): Enviar comandos ccTalk para que los motores escupan monedas físicas.

3. Escáner de Pasaporte / INE (MEPR500+)

Integración: Consumo de SDK propietario.

Métodos de Prueba Requeridos:

Test_EscanearDocumento(): Inicializar el SDK, colocar un documento físico, disparar la lectura y hacer un Console.WriteLine con los datos extraídos (Nombre, ID, Nacionalidad).

4. Impresora Térmica (KP-80A)

Integración: Comandos estándar ESC/POS por puerto Serial/USB.

Métodos de Prueba Requeridos:

Test_ImprimirVoucherPrueba(): Mandar una cadena de texto "Hola Mundo FINSO" con comandos ESC/POS (negritas) al puerto correspondiente y ejecutar el comando de corte de papel.

5. Sensores y Seguridad (Módulo ALB02)

Integración: Escucha Serial Reactiva (Puerto COM).

Métodos de Prueba Requeridos:

Test_EscucharSensores(): Iniciar un loop en consola que lea el puerto COM y lance un Console.WriteLine("¡PUERTA ABIERTA!") o Console.WriteLine("¡VIBRACIÓN DETECTADA!") cuando se manipule físicamente el gabinete.

🎯 Estructura Esperada (Ejemplo Conceptual)

El entregable final se verá similar a esto:

// Program.cs
public static void Main(string[] args)
{
    Console.WriteLine("Iniciando Pruebas de Hardware FINSO...");

    // Descomentar la prueba que se desee ejecutar frente a la máquina:
    
    // HardwareTests.Test_ImprimirVoucherPrueba();
    // HardwareTests.Test_PingMonederos();
    // HardwareTests.Test_DispensarMonedas(2, 1); // Dos monedas de $1, una de $5
    // HardwareTests.Test_EscanearDocumento();
    // HardwareTests.Test_AceptarBilletes();
    // HardwareTests.Test_EscucharSensores();

    Console.ReadLine();
}





