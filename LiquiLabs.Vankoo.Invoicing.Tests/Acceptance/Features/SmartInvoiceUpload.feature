@US01
Feature: Smart invoice upload
  As a MYPE business owner
  I want to register my invoice in the MYPE Web
  So that the system extracts its data and prepares the liquidity operation

  Background:
    Given a MYPE business owner is signed in through the API Gateway

  Scenario Outline: An invoice with inconsistent data is not sent to the auction
    Given the business owner's company KYC is "VERIFIED"
    And the business owner has a readable PDF invoice
    And the OCR reads the invoice with <inconsistency>
    When the business owner uploads the invoice
    And the system finishes the extraction and initial validation
    Then the invoice status is "<status>"
    And the invoice reports the issue "<issue code>"
    And the invoice is not sent to the auction

    Examples:
      | inconsistency                     | status          | issue code              |
      | a total that does not reconcile   | REQUIRES_REVIEW | TOTALS_DO_NOT_RECONCILE |
      | a low-confidence due date         | REQUIRES_REVIEW | LOW_OCR_CONFIDENCE      |
      | an expired due date               | NOT_ELIGIBLE    | INVOICE_EXPIRED         |
      | the same RUC for issuer and payer | NOT_ELIGIBLE    | ISSUER_EQUALS_PAYER     |

  Scenario Outline: A MYPE whose company is not verified cannot upload invoices
    Given the business owner's company KYC is "<kyc status>"
    And the business owner has a readable PDF invoice
    When the business owner tries to upload the invoice
    Then the upload is refused with "MYPE_NOT_VERIFIED"
    And no invoice is stored

    Examples:
      | kyc status |
      | UNKNOWN    |
      | PENDING    |
      | IN_REVIEW  |
      | REJECTED   |
