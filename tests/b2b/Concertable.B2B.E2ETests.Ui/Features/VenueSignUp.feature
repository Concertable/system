Feature: Venue manager sign-up
  A new venue manager registers via the business gateway, signs in, creates their venue.

  @SignUp @VenueManager
  Scenario: New venue manager registers, signs in, creates their venue
    Given a visitor is on the business gateway
    When they click get started as a venue
    And they click the sign up link
    And they register as VenueManager
    And they sign in with their new credentials
    And they fill in the create venue form
    And they submit the create venue form
    Then they land on the venue surface authenticated
