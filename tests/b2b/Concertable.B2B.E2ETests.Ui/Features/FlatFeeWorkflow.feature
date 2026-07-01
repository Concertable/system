Feature: FlatFee workflow happy path
  A venue manager posts a flat fee opportunity, an artist applies, the venue
  manager accepts and pays the flat fee. The booking lands in escrow and a
  draft concert is created.

  @VenueManager @ResetsStripe
  Scenario: Venue manager books artist on a flat fee
    When the venue manager posts a flat fee opportunity for £500
    And the artist applies to the opportunity
    And the venue manager accepts and pays with a valid card
    Then a draft concert is created

  @VenueManager
  Scenario: Venue manager books artist on a flat fee with a new card
    Given a flat fee opportunity has been applied to
    When the venue manager pays the flat fee with a new card
    Then a draft concert is created

  @VenueManager @PaymentFailure
  Scenario: Venue manager flat fee attempt is declined
    Given a flat fee opportunity has been applied to
    When the venue manager pays the flat fee with a declined card
    Then the payment is rejected

  @VenueManager @PaymentFailure
  Scenario: Venue manager completes 3DS challenge on flat fee
    Given a flat fee opportunity has been applied to
    When the venue manager pays the flat fee with a 3DS card
    Then a draft concert is created

  @VenueManager @PaymentFailure
  Scenario: Venue manager 3DS authentication fails on flat fee
    Given a flat fee opportunity has been applied to
    When the venue manager pays the flat fee with a 3DS-failing card
    Then the payment is rejected
