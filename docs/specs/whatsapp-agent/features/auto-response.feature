# language: en
Feature: Respuesta automática del agente WhatsApp
  Como asesor de Accesorios Para Él
  Quiero que el bot responda automáticamente preguntas frecuentes
  Para atender consultas 24/7 sin intervención manual

  Background:
    Given existe un contacto con WaId "51987654321" y BotHandling activado
    And el servicio de agente está configurado con ANTHROPIC_API_KEY
    And el sistema tiene el prompt de negocio de Accesorios Para Él cargado

  # ---------------------------------------------------------------------------
  # Saludos y primer contacto
  # ---------------------------------------------------------------------------

  Scenario: Nuevo contacto WA recibe saludo del bot
    Given no existe ningún contacto con WaId "51900000001"
    When llega un webhook inbound de WhatsApp del WaId "51900000001" con mensaje "Hola"
    Then el sistema crea un Contact nuevo con WaId "51900000001" y BotHandling true
    And el agente genera una respuesta de saludo en español
    And se registra una actividad outbound de tipo "whatsapp_outbound" para el contacto
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # FAQ — Envíos
  # ---------------------------------------------------------------------------

  Scenario: Bot responde consulta sobre envíos a provincias
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿envían a Arequipa?"
    Then el agente genera una respuesta que menciona "2-3 días hábiles"
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  Scenario: Bot responde consulta sobre envíos internacionales
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿hacen envíos al extranjero?"
    Then el agente genera una respuesta que confirma envíos internacionales
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones

  # ---------------------------------------------------------------------------
  # FAQ — Métodos de pago
  # ---------------------------------------------------------------------------

  Scenario: Bot responde consulta sobre métodos de pago
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿cómo puedo pagar?"
    Then el agente genera una respuesta que menciona al menos dos de: "Yape", "Plin", "transferencia", "tarjeta", "contra entrega"
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # FAQ — Separados
  # ---------------------------------------------------------------------------

  Scenario: Bot responde consulta sobre separado o apartado
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿puedo separar un producto?"
    Then el agente genera una respuesta que menciona "S/. 40" como depósito mínimo
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # FAQ — Política de cambios
  # ---------------------------------------------------------------------------

  Scenario: Bot responde consulta sobre política de cambios o devoluciones
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿aceptan devoluciones?"
    Then el agente genera una respuesta que menciona "15 días"
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # FAQ — Horario de atención
  # ---------------------------------------------------------------------------

  Scenario: Bot responde consulta sobre horario de atención
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿a qué hora atienden?"
    Then el agente genera una respuesta que menciona "9" y "7" como horarios de apertura y cierre
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # FAQ — Colecciones de productos
  # ---------------------------------------------------------------------------

  Scenario Outline: Bot responde consultas sobre colecciones de brazaletes
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿tienen la colección <coleccion>?"
    Then el agente genera una respuesta que menciona "<coleccion>"
    And la respuesta indica que el producto está disponible
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

    Examples:
      | coleccion   | publico_objetivo |
      | METROPOLE   | caballero        |
      | BOSS        | caballero        |
      | E.ARMANI    | caballero        |
      | CROCODILE   | caballero        |
      | ANGEL EYES  | dama             |

  # ---------------------------------------------------------------------------
  # Deflexión — preguntas fuera de contexto
  # ---------------------------------------------------------------------------

  Scenario: Bot deflecta pregunta sobre temas tecnológicos fuera de catálogo
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿cómo programo en Python?"
    Then el agente genera una respuesta que redirige al catálogo de accesorios
    And la respuesta no intenta responder sobre programación
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # Redirección — productos fuera de catálogo
  # ---------------------------------------------------------------------------

  Scenario: Bot redirige solicitud de producto fuera de catálogo a productos disponibles
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿tienen anillos o relojes?"
    Then el agente genera una respuesta que indica que ese producto no está disponible
    And la respuesta menciona al menos una colección disponible del catálogo
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis
