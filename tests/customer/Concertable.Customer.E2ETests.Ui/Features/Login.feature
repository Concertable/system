Feature: Login
  Smoke test that the OIDC redirect dance completes end-to-end via SPA + Auth host.

  Scenario: Customer signs in via OIDC
    Given a visitor is on the home page
    When they click sign in
    And they submit seeded customer credentials
    Then they are returned to the home page
