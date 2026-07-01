Feature: Ticket purchase happy path (mobile)
  A customer browses the home screen on the mobile app, opens a concert,
  purchases a ticket through the Stripe payment sheet, and views the QR code
  on the ticket detail screen.

  @Customer
  Scenario: Customer opens a concert, purchases a ticket, and views the QR code
    Given the customer is signed in
    When the customer opens the first concert
    Then the customer should be on the concert detail screen
    When the customer taps buy tickets
    Then the customer should be on the checkout screen
    When the customer pays with a test card
    Then the checkout success screen should be visible
    When the customer taps view tickets
    Then the customer should be on the tickets screen
    And a ticket card should be listed
    When the customer opens the first ticket
    Then the QR code should be visible
