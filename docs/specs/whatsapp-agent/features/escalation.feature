# language: en
Feature: Escalación a asesor humano
  Como asesor de Accesorios Para Él
  Quiero que el bot escale a un humano cuando el cliente lo solicite
  Para garantizar atención personalizada cuando sea necesaria

  Background:
    Given existe un contacto con WaId "51987654321" y BotHandling activado
    And el servicio de agente está configurado con ANTHROPIC_API_KEY
    And el sistema tiene el prompt de negocio de Accesorios Para Él cargado

  # ---------------------------------------------------------------------------
  # Escalación explícita por solicitud del cliente
  # ---------------------------------------------------------------------------

  Scenario: Cliente solicita hablar con un asesor humano
    When llega un webhook inbound del WaId "51987654321" con mensaje "quiero hablar con alguien"
    Then el agente retorna la señal "ESCALAR"
    And el sistema NO envía ningún mensaje outbound al WaId "51987654321"
    And NO se registra ninguna actividad de tipo "whatsapp_outbound" para el contacto
    And sí se registra una actividad de tipo "whatsapp_inbound" con el mensaje recibido
    And el webhook retorna HTTP 200

  Scenario: Cliente solicita hablar con alguien usando variante de frase
    When llega un webhook inbound del WaId "51987654321" con mensaje "quiero un asesor"
    Then el agente retorna la señal "ESCALAR"
    And el sistema NO envía ningún mensaje outbound al WaId "51987654321"
    And NO se registra ninguna actividad de tipo "whatsapp_outbound" para el contacto
    And el webhook retorna HTTP 200

  Scenario: Cliente solicita hablar con alguien usando frase directa
    When llega un webhook inbound del WaId "51987654321" con mensaje "prefiero hablar con una persona"
    Then el agente retorna la señal "ESCALAR"
    And el sistema NO envía ningún mensaje outbound al WaId "51987654321"
    And NO se registra ninguna actividad de tipo "whatsapp_outbound" para el contacto
    And el webhook retorna HTTP 200

  # ---------------------------------------------------------------------------
  # Retorno null del agente — webhook aún responde 200
  # ---------------------------------------------------------------------------

  Scenario: El agente retorna null y el webhook retorna 200 sin fallo
    Given el servicio de agente está configurado para retornar null para cualquier mensaje
    When llega un webhook inbound del WaId "51987654321" con mensaje "consulta genérica"
    Then el agente retorna null
    And el sistema NO envía ningún mensaje outbound al WaId "51987654321"
    And NO se registra ninguna actividad de tipo "whatsapp_outbound"
    And sí se registra una actividad de tipo "whatsapp_inbound"
    And el webhook retorna HTTP 200

  # ---------------------------------------------------------------------------
  # Mensaje ofensivo o iracundo — respuesta empática antes de escalar
  # ---------------------------------------------------------------------------

  Scenario: Cliente envía mensaje de enojo y el bot responde con empatía antes de escalar
    When llega un webhook inbound del WaId "51987654321" con mensaje "¡Qué pésimo servicio, ya estoy harto de esperar!"
    Then el agente genera una respuesta empática en español que reconoce la molestia del cliente
    And la respuesta no minimiza la queja del cliente
    And la respuesta indica que un asesor lo atenderá
    And se registra una actividad outbound de tipo "whatsapp_outbound" con la respuesta empática
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis
    And después de enviar la respuesta empática el asesor puede intervenir manualmente
