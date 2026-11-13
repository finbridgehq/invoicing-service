@US01
Feature: Confirm an invoice review
  As a MYPE business owner
  I want to confirm or correct low-confidence OCR data
  So that my invoice can continue only when the confirmed data is consistent

  Background:
    Given a verified MYPE has an invoice awaiting review after low-confidence OCR

  Scenario: Matching confirmed data completes the consistency check
    When the MYPE confirms the extracted invoice data
    Then the confirmed invoice status is "CONSISTENCY_PASSED"

  Scenario: Incorrect confirmed totals keep the invoice under review
    When the MYPE confirms an incorrect invoice total
    Then the confirmed invoice status is "REQUIRES_REVIEW"
    And the confirmation reports the issue "TOTALS_DO_NOT_RECONCILE"

  Scenario: Another MYPE cannot confirm the invoice
    When another MYPE tries to confirm the invoice
    Then the confirmation is rejected with InvoiceNotFoundException
