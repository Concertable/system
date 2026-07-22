Feature: Organization member management
  A venue owner invites a colleague, who accepts through the emailed link and is
  then managed from the members page. A user who belongs to more than one
  organization uses the tenant switcher to scope member management to the chosen
  organization.

  @VenueManager
  Scenario: Owner invites a member who accepts and is then managed
    Given the venue owner is on the members page
    When the owner invites a colleague to the organization
    And the colleague accepts the invitation through the emailed link
    And the owner returns to the members page
    Then the colleague appears in the member roster
    When the owner changes the colleague's role to Finance
    Then the colleague's role shows as Finance
    When the owner removes the colleague
    Then the colleague no longer appears in the roster

  @VenueManager
  Scenario: Switching organization scopes member management to the chosen tenant
    Given the venue owner is on the members page
    And the owner invites a colleague to the organization
    And the colleague accepts the invitation through the emailed link
    Then the tenant switcher offers the colleague both organizations
    When the colleague switches to their own organization
    Then member management shows only their own organization's members
