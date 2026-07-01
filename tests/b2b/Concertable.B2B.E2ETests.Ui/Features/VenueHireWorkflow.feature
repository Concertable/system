Feature: VenueHire workflow happy path
  A venue manager posts a venue hire opportunity, an artist pays the hire fee
  upfront to apply, the venue manager accepts (no further payment). A draft concert is created.

  @VenueManager @ResetsStripe
  Scenario: Artist pays hire fee upfront to book venue
    When the venue manager posts a venue hire opportunity for £300
    And the artist applies to the venue hire opportunity with a valid card
    And the venue manager accepts the application
    Then a draft concert is created
    And a payment hold of £300 is captured from the artist

  @ArtistManager
  Scenario: Artist pays hire fee upfront with a new card
    Given a venue hire opportunity is open for application
    When the artist pays the venue hire fee with a new card
    And the venue manager accepts the application
    Then a draft concert is created
    And a payment hold of £250 is captured from the artist

  @ArtistManager @PaymentFailure
  Scenario: Artist venue hire attempt is declined
    Given a venue hire opportunity is open for application
    When the artist pays the venue hire fee with a declined card
    Then the payment is rejected

  @ArtistManager @PaymentFailure
  Scenario: Artist completes 3DS challenge on venue hire
    Given a venue hire opportunity is open for application
    When the artist pays the venue hire fee with a 3DS card
    And the venue manager accepts the application
    Then a draft concert is created

  @ArtistManager @PaymentFailure
  Scenario: Artist 3DS authentication fails on venue hire
    Given a venue hire opportunity is open for application
    When the artist pays the venue hire fee with a 3DS-failing card
    Then the payment is rejected
