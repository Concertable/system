Feature: Customer sign-up
  A new customer registers via the customer surface, signs in, and lands on the home page.

  @SignUp @Customer
  Scenario: New customer registers and signs in
    Given a visitor is on the customer home page
    When they go to sign in
    And they click the sign up link
    And they register with a new email
    And they sign in with their new credentials
    Then they are returned to the customer home page authenticated
