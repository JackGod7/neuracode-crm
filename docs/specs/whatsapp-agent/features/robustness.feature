# language: en
Feature: Robustez y casos límite del webhook WhatsApp
  Como desarrollador del sistema Neuracode CRM
  Quiero que el webhook sea robusto ante casos anómalos
  Para garantizar que el endpoint siempre retorne 200 y no genere efectos duplicados

  Background:
    Given existe un contacto con WaId "51987654321" y BotHandling activado
    And el servicio de agente está configurado con ANTHROPIC_API_KEY

  # ---------------------------------------------------------------------------
  # Idempotencia por wamid
  # ---------------------------------------------------------------------------

  Scenario: Webhook con wamid duplicado es ignorado sin efectos secundarios
    Given el sistema ya procesó el mensaje con Wamid "wamid.abc123"
    And existe una actividad "whatsapp_inbound" registrada para ese Wamid
    When llega un segundo webhook inbound del WaId "51987654321" con el mismo Wamid "wamid.abc123"
    Then el sistema NO crea una segunda actividad "whatsapp_inbound"
    And el sistema NO invoca al agente de IA
    And el sistema NO envía ningún mensaje outbound
    And el webhook retorna HTTP 200

  # ---------------------------------------------------------------------------
  # Timeout del agente
  # ---------------------------------------------------------------------------

  Scenario: Timeout del agente mayor a 3 segundos no falla el webhook
    Given el servicio de agente está configurado para tardar más de 3 segundos en responder
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿cuánto cuesta?"
    Then el sistema cancela la llamada al agente por timeout
    And el sistema NO envía ningún mensaje outbound al WaId "51987654321"
    And NO se registra ninguna actividad de tipo "whatsapp_outbound"
    And sí se registra una actividad de tipo "whatsapp_inbound"
    And el webhook retorna HTTP 200

  # ---------------------------------------------------------------------------
  # Mensajes en otros idiomas
  # ---------------------------------------------------------------------------

  Scenario: Mensaje en inglés recibe respuesta en español
    When llega un webhook inbound del WaId "51987654321" con mensaje "Do you ship internationally?"
    Then el agente genera una respuesta únicamente en español
    And la respuesta no contiene oraciones en inglés
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # Mensajes con solo emojis
  # ---------------------------------------------------------------------------

  Scenario: Mensaje compuesto únicamente por emojis recibe respuesta coherente
    When llega un webhook inbound del WaId "51987654321" con mensaje "❤️👌🛒"
    Then el agente genera una respuesta coherente en español
    And la respuesta no refleja confusión ni error
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el mensaje enviado contiene como máximo 2 oraciones
    And el mensaje enviado no contiene emojis

  # ---------------------------------------------------------------------------
  # Mensajes muy largos
  # ---------------------------------------------------------------------------

  Scenario: Mensaje de más de 500 caracteres no provoca fallo en el agente
    Given un mensaje inbound con 520 caracteres de texto en español
    When llega un webhook inbound del WaId "51987654321" con ese mensaje largo
    Then el agente procesa el mensaje sin lanzar excepciones
    And el webhook retorna HTTP 200
    And si el agente genera respuesta, tiene como máximo 2 oraciones y no contiene emojis

  # ---------------------------------------------------------------------------
  # Mensajes rápidos consecutivos del mismo contacto
  # ---------------------------------------------------------------------------

  Scenario: Dos mensajes rápidos del mismo contacto no generan actividades outbound duplicadas
    Given el sistema procesa dos mensajes inbound consecutivos del WaId "51987654321"
    And el primer mensaje tiene Wamid "wamid.first001" y texto "¿tienen envíos?"
    And el segundo mensaje tiene Wamid "wamid.second002" y texto "¿cuánto tarda?"
    When ambos webhooks llegan en un intervalo de menos de 2 segundos
    Then el sistema registra exactamente 2 actividades de tipo "whatsapp_inbound"
    And se registra al menos 1 actividad de tipo "whatsapp_outbound"
    And NO se registran actividades "whatsapp_outbound" con Wamid duplicado
    And el webhook retorna HTTP 200 para cada llamada
