Feature: Versus workflow happy path
  A venue manager posts a versus opportunity (guarantee + door percentage), an artist
  applies, the venue manager accepts and registers a card for future settlement. A draft
  concert is created.

  @VenueManager @ResetsStripe
  Scenario: Venue manager books artist on a versus deal
    When the venue manager posts a versus opportunity for £100 guarantee and 70% door
    And the artist applies to the opportunity
    And the venue manager accepts and registers a valid card
    Then a draft concert is created

  @VenueManager
  Scenario: Venue manager books artist on a versus deal with a new card
    Given a versus opportunity has been applied to
    When the venue manager registers a card with a new card
    Then a draft concert is created

  @VenueManager @PaymentFailure
  Scenario: Venue manager versus card registration is declined
    Given a versus opportunity has been applied to
    When the venue manager registers a card with a declined card
    Then the payment is rejected

  @VenueManager @PaymentFailure
  Scenario: Venue manager completes 3DS challenge on versus
    Given a versus opportunity has been applied to
    When the venue manager registers a card with a 3DS card
    Then a draft concert is created

  @VenueManager @PaymentFailure
  Scenario: Venue manager 3DS authentication fails on versus
    Given a versus opportunity has been applied to
    When the venue manager registers a card with a 3DS-failing card
    Then the payment is rejected
