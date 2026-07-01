Feature: Login
  Smoke test that the OIDC redirect dance completes end-to-end via SPA + Auth host.

  Scenario: Venue manager signs in via OIDC
    Given a visitor is on the business home page
    When they click sign in
    And they submit seeded venue manager credentials
    Then they are returned to the business home page
