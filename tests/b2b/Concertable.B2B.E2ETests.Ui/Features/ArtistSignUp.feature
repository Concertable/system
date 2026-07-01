Feature: Artist manager sign-up
  A new artist manager registers via the business gateway, signs in, creates their artist profile.

  @SignUp @ArtistManager
  Scenario: New artist manager registers, signs in, creates their artist profile
    Given a visitor is on the business gateway
    When they click get started as an artist
    And they click the sign up link
    And they register as ArtistManager
    And they sign in with their new credentials
    And they fill in the create artist form
    And they submit the create artist form
    Then they land on the artist surface authenticated
