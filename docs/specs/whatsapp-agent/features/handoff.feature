# language: en
Feature: Handoff y bot-resume por contacto
  Como asesor de Accesorios Para Él
  Quiero poder tomar el control de una conversación y devolverla al bot
  Para garantizar atención humana cuando el cliente lo necesita

  Background:
    Given el servicio de agente está configurado con ANTHROPIC_API_KEY

  # ---------------------------------------------------------------------------
  # Handoff — desactivar bot para un contacto
  # ---------------------------------------------------------------------------

  Scenario: POST /handoff establece BotHandling en false para el contacto
    Given existe un contacto con id "contact-abc" y WaId "51987654321" y BotHandling true
    When el asesor hace POST a "/api/contacts/contact-abc/handoff"
    Then el sistema responde HTTP 200 con body "{ success: true, botHandling: false }"
    And el contacto con id "contact-abc" tiene BotHandling false en la base de datos

  Scenario: Después del handoff, un mensaje inbound no activa la respuesta del bot
    Given existe un contacto con id "contact-abc" y WaId "51987654321" y BotHandling false
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿tienen envíos?"
    Then el sistema guarda la actividad de tipo "whatsapp_inbound"
    And el sistema NO invoca al agente de IA para ese mensaje
    And el sistema NO envía ningún mensaje outbound al WaId "51987654321"
    And NO se registra ninguna actividad de tipo "whatsapp_outbound"
    And el webhook retorna HTTP 200

  # ---------------------------------------------------------------------------
  # Bot-resume — reactivar bot para un contacto
  # ---------------------------------------------------------------------------

  Scenario: POST /bot-resume establece BotHandling en true para el contacto
    Given existe un contacto con id "contact-abc" y WaId "51987654321" y BotHandling false
    When el asesor hace POST a "/api/contacts/contact-abc/bot-resume"
    Then el sistema responde HTTP 200 con body "{ success: true, botHandling: true }"
    And el contacto con id "contact-abc" tiene BotHandling true en la base de datos

  Scenario: Después del bot-resume, un mensaje inbound activa la respuesta del bot
    Given existe un contacto con id "contact-abc" y WaId "51987654321" y BotHandling true
    When llega un webhook inbound del WaId "51987654321" con mensaje "¿cómo pagan?"
    Then el sistema invoca al agente de IA con el mensaje del contacto
    And el agente genera una respuesta sobre métodos de pago
    And se registra una actividad outbound de tipo "whatsapp_outbound"
    And el webhook retorna HTTP 200

  # ---------------------------------------------------------------------------
  # Errores — contacto no encontrado
  # ---------------------------------------------------------------------------

  Scenario: Handoff sobre contacto inexistente retorna 404
    Given no existe ningún contacto con id "contact-inexistente"
    When el asesor hace POST a "/api/contacts/contact-inexistente/handoff"
    Then el sistema responde HTTP 404
    And el body de respuesta indica que el contacto no fue encontrado

  Scenario: Bot-resume sobre contacto inexistente retorna 404
    Given no existe ningún contacto con id "contact-inexistente"
    When el asesor hace POST a "/api/contacts/contact-inexistente/bot-resume"
    Then el sistema responde HTTP 404
    And el body de respuesta indica que el contacto no fue encontrado
