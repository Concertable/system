Feature: DoorSplit workflow happy path
  A venue manager posts a door-split opportunity, an artist applies, the venue
  manager accepts and registers a card for future settlement. A draft concert is created.

  @VenueManager @ResetsStripe
  Scenario: Venue manager books artist on a door split
    When the venue manager posts a door split opportunity for 70% door
    And the artist applies to the opportunity
    And the venue manager accepts and registers a valid card
    Then a draft concert is created

  @VenueManager
  Scenario: Venue manager books artist on a door split with a new card
    Given a door split opportunity has been applied to
    When the venue manager registers a card with a new card
    Then a draft concert is created

  @VenueManager @PaymentFailure
  Scenario: Venue manager door split card registration is declined
    Given a door split opportunity has been applied to
    When the venue manager registers a card with a declined card
    Then the payment is rejected

  @VenueManager @PaymentFailure
  Scenario: Venue manager completes 3DS challenge on door split
    Given a door split opportunity has been applied to
    When the venue manager registers a card with a 3DS card
    Then a draft concert is created

  @VenueManager @PaymentFailure
  Scenario: Venue manager 3DS authentication fails on door split
    Given a door split opportunity has been applied to
    When the venue manager registers a card with a 3DS-failing card
    Then the payment is rejected
